-- 002_usp_SubmitContact.sql
-- -------------------------
-- Creates or replaces the only procedure the contact API will call.
--
-- Run this second, connected to ssportfolio, not master. This file is one
-- batch on purpose: SQL Server requires CREATE PROCEDURE to be the only
-- statement in its batch, and the portal Query editor cannot run GO.
--
-- Before you run it, check the database name shown in the query editor.
-- If you run it in master by mistake, switch to ssportfolio and run:
--
--     DROP PROCEDURE IF EXISTS dbo.usp_SubmitContact;
--
-- in master, then run this file again in ssportfolio.
--
-- Safe to run again. CREATE OR ALTER replaces the procedure body.

CREATE OR ALTER PROCEDURE dbo.usp_SubmitContact
    @Name nvarchar(200),
    @Email nvarchar(320),
    @Message nvarchar(4000),
    @IpHash varbinary(64),
    -- Defaults are the product limits. A later admin tool can pass other
    -- numbers without editing this body. Tests do that too.
    @IpBurstLimit int = 3,
    @IpBurstMinutes int = 10,
    @IpDailyLimit int = 10,
    @GlobalDailyLimit int = 50,
    @WindowHours int = 24,
    @RetentionHours int = 48
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Result codes the API should map:
    --
    -- 0 accepted              message stored, MessageId is set
    -- 1 rate_limited_ip       this hash is over its burst or daily limit
    -- 2 rate_limited_global   the whole site is over the daily cap
    -- 3 invalid               input failed the length or shape checks
    -- 5 busy                  another call held the lock; try again shortly
    --
    -- Reason adds detail the code does not: ip_burst, ip_daily, or a
    -- short explanation for invalid input.
    -- RetryAfterSeconds is how long to wait before one slot opens.
    DECLARE @ResultCode int;
    DECLARE @ResultName nvarchar(40);
    DECLARE @RetryAfterSeconds int;
    DECLARE @MessageId bigint;
    DECLARE @Reason nvarchar(200);
    DECLARE @Now datetime2(3) = SYSUTCDATETIME();

    DECLARE @NameClean nvarchar(200) = NULLIF(LTRIM(RTRIM(@Name)), N'');
    DECLARE @EmailClean nvarchar(320) = NULLIF(LTRIM(RTRIM(@Email)), N'');
    DECLARE @MessageClean nvarchar(4000) = NULLIF(LTRIM(RTRIM(@Message)), N'');

    IF @Name IS NULL OR @NameClean IS NULL OR LEN(@NameClean) > 100
        SET @Reason = N'Name must be 1 to 100 characters.';
    ELSE IF @Email IS NULL OR @EmailClean IS NULL OR LEN(@EmailClean) > 254
        OR @EmailClean NOT LIKE N'%_@_%._%'
        OR @EmailClean LIKE N'% %'
        SET @Reason = N'Email must be 254 characters or fewer and look like an email address.';
    ELSE IF @Message IS NULL OR @MessageClean IS NULL OR LEN(@MessageClean) < 10 OR LEN(@MessageClean) > 2000
        SET @Reason = N'Message must be 10 to 2000 characters.';
    ELSE IF @IpHash IS NULL OR DATALENGTH(@IpHash) <> 32
        SET @Reason = N'IP hash must be exactly 32 bytes (raw HMAC-SHA256).';
    ELSE IF @IpBurstLimit IS NULL OR @IpBurstLimit < 1
        OR @IpDailyLimit IS NULL OR @IpDailyLimit < 1
        OR @GlobalDailyLimit IS NULL OR @GlobalDailyLimit < 1
        OR @IpBurstMinutes IS NULL OR @IpBurstMinutes < 1
        OR @WindowHours IS NULL OR @WindowHours < 1
        OR @RetentionHours IS NULL OR @RetentionHours < 1
        SET @Reason = N'Limit parameters must be at least 1.';

    IF @Reason IS NOT NULL
    BEGIN
        SELECT
            CAST(3 AS int) AS ResultCode,
            N'invalid' AS ResultName,
            CAST(NULL AS int) AS RetryAfterSeconds,
            CAST(NULL AS bigint) AS MessageId,
            @Reason AS Reason;
        RETURN 3;
    END;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- One lock for every caller. The site accepts about 50 messages a
        -- day, so serializing them is cheap and makes the counts exact.
        -- Two requests at the same moment cannot both read "49" and both
        -- insert. The per-IP check uses the same lock, which also avoids
        -- lock-order deadlocks.
        DECLARE @LockResult int;
        EXEC @LockResult = sys.sp_getapplock
            @Resource = N'usp_SubmitContact',
            @LockMode = N'Exclusive',
            @LockOwner = N'Transaction',
            @LockTimeout = 5000;

        IF @LockResult < 0
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT
                CAST(5 AS int) AS ResultCode,
                N'busy' AS ResultName,
                CAST(5 AS int) AS RetryAfterSeconds,
                CAST(NULL AS bigint) AS MessageId,
                N'lock_timeout' AS Reason;
            RETURN 5;
        END;

        -- Drop old hit rows while we hold the lock. 48 hours is longer
        -- than the 24-hour window, so cleanup cannot change a decision.
        DELETE FROM dbo.ContactRateLimitHit
        WHERE HitAtUtc < DATEADD(hour, -@RetentionHours, @Now);

        DECLARE @BurstStart datetime2(3) = DATEADD(minute, -@IpBurstMinutes, @Now);
        DECLARE @DayStart datetime2(3) = DATEADD(hour, -@WindowHours, @Now);
        DECLARE @BurstCount int;
        DECLARE @DailyCount int;
        DECLARE @GlobalCount int;

        -- HOLDLOCK keeps these reads stable until the transaction ends.
        -- The application lock above is the real guard. HOLDLOCK is the
        -- backup if a future edit removes that lock.
        SELECT @BurstCount = COUNT(*)
        FROM dbo.ContactRateLimitHit WITH (HOLDLOCK)
        WHERE IpHash = @IpHash
          AND HitAtUtc > @BurstStart;

        SELECT @DailyCount = COUNT(*)
        FROM dbo.ContactRateLimitHit WITH (HOLDLOCK)
        WHERE IpHash = @IpHash
          AND HitAtUtc > @DayStart;

        SELECT @GlobalCount = COUNT(*)
        FROM dbo.ContactRateLimitHit WITH (HOLDLOCK)
        WHERE HitAtUtc > @DayStart;

        IF @BurstCount >= @IpBurstLimit
        BEGIN
            -- Age out enough of the oldest hits in this window that one
            -- more submission would fit. With the usual numbers, that is
            -- the single oldest hit in the 10-minute window.
            DECLARE @BurstSlot datetime2(3);
            SELECT @BurstSlot = HitAtUtc
            FROM (
                SELECT
                    HitAtUtc,
                    ROW_NUMBER() OVER (ORDER BY HitAtUtc ASC, Id ASC) AS Slot
                FROM dbo.ContactRateLimitHit
                WHERE IpHash = @IpHash
                  AND HitAtUtc > @BurstStart
            ) AS ranked
            WHERE Slot = @BurstCount - @IpBurstLimit + 1;

            SET @RetryAfterSeconds = DATEDIFF(second, @Now, DATEADD(minute, @IpBurstMinutes, @BurstSlot));
            IF @RetryAfterSeconds IS NULL OR @RetryAfterSeconds < 1
                SET @RetryAfterSeconds = 1;

            COMMIT TRANSACTION;
            SELECT
                CAST(1 AS int) AS ResultCode,
                N'rate_limited_ip' AS ResultName,
                @RetryAfterSeconds AS RetryAfterSeconds,
                CAST(NULL AS bigint) AS MessageId,
                N'ip_burst' AS Reason;
            RETURN 1;
        END;

        IF @DailyCount >= @IpDailyLimit
        BEGIN
            DECLARE @DailySlot datetime2(3);
            SELECT @DailySlot = HitAtUtc
            FROM (
                SELECT
                    HitAtUtc,
                    ROW_NUMBER() OVER (ORDER BY HitAtUtc ASC, Id ASC) AS Slot
                FROM dbo.ContactRateLimitHit
                WHERE IpHash = @IpHash
                  AND HitAtUtc > @DayStart
            ) AS ranked
            WHERE Slot = @DailyCount - @IpDailyLimit + 1;

            SET @RetryAfterSeconds = DATEDIFF(second, @Now, DATEADD(hour, @WindowHours, @DailySlot));
            IF @RetryAfterSeconds IS NULL OR @RetryAfterSeconds < 1
                SET @RetryAfterSeconds = 1;

            COMMIT TRANSACTION;
            SELECT
                CAST(1 AS int) AS ResultCode,
                N'rate_limited_ip' AS ResultName,
                @RetryAfterSeconds AS RetryAfterSeconds,
                CAST(NULL AS bigint) AS MessageId,
                N'ip_daily' AS Reason;
            RETURN 1;
        END;

        IF @GlobalCount >= @GlobalDailyLimit
        BEGIN
            DECLARE @GlobalSlot datetime2(3);
            SELECT @GlobalSlot = HitAtUtc
            FROM (
                SELECT
                    HitAtUtc,
                    ROW_NUMBER() OVER (ORDER BY HitAtUtc ASC, Id ASC) AS Slot
                FROM dbo.ContactRateLimitHit
                WHERE HitAtUtc > @DayStart
            ) AS ranked
            WHERE Slot = @GlobalCount - @GlobalDailyLimit + 1;

            SET @RetryAfterSeconds = DATEDIFF(second, @Now, DATEADD(hour, @WindowHours, @GlobalSlot));
            IF @RetryAfterSeconds IS NULL OR @RetryAfterSeconds < 1
                SET @RetryAfterSeconds = 1;

            COMMIT TRANSACTION;
            SELECT
                CAST(2 AS int) AS ResultCode,
                N'rate_limited_global' AS ResultName,
                @RetryAfterSeconds AS RetryAfterSeconds,
                CAST(NULL AS bigint) AS MessageId,
                N'global_cap' AS Reason;
            RETURN 2;
        END;

        INSERT INTO dbo.ContactRateLimitHit (IpHash, HitAtUtc)
        VALUES (@IpHash, @Now);

        DECLARE @Inserted TABLE (Id bigint);

        INSERT INTO dbo.ContactMessage (Name, Email, Message, CreatedUtc, IpHash, Status, IsFlagged)
        OUTPUT inserted.Id INTO @Inserted (Id)
        VALUES (@NameClean, @EmailClean, @MessageClean, @Now, @IpHash, N'new', 0);

        SELECT @MessageId = Id FROM @Inserted;

        COMMIT TRANSACTION;

        SELECT
            CAST(0 AS int) AS ResultCode,
            N'accepted' AS ResultName,
            CAST(NULL AS int) AS RetryAfterSeconds,
            @MessageId AS MessageId,
            CAST(NULL AS nvarchar(200)) AS Reason;
        RETURN 0;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
