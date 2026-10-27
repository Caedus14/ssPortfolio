/*
    001_tables.sql
    --------------
    Creates the two tables the contact form will use.

    Run this first, in the Azure portal Query editor, while you are connected
    to the ssportfolio database (not master). The whole file is one batch.
    There is no GO. The portal query editor does not understand GO.

    Safe to run again. If the tables already exist, this script leaves them
    alone. It does not alter columns on a table that already exists.
*/

IF DB_NAME() = N'master'
    THROW 51002, N'Connect to the ssportfolio database, not master, then run this script again.', 1;

/*
    ContactMessage is the inbox.

    The API never stores the visitor's IP address. It stores a 32-byte
    HMAC-SHA256 of a normalized IP string. The HMAC key stays in the API
    settings, not in this database. IPv6 addresses are grouped by /64
    before hashing, so one network prefix shares one hash. That grouping
    happens in the API. This table only stores the finished hash.

    Status and IsFlagged are for a later admin dashboard. The submit
    procedure always inserts Status = 'new' and IsFlagged = 0.
*/
IF OBJECT_ID(N'dbo.ContactMessage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ContactMessage
    (
        Id bigint IDENTITY(1, 1) NOT NULL
            CONSTRAINT PK_ContactMessage PRIMARY KEY,
        Name nvarchar(100) NOT NULL,
        Email nvarchar(254) NOT NULL,
        Message nvarchar(2000) NOT NULL,
        CreatedUtc datetime2(3) NOT NULL
            CONSTRAINT DF_ContactMessage_CreatedUtc DEFAULT (SYSUTCDATETIME()),
        IpHash binary(32) NOT NULL,
        Status nvarchar(20) NOT NULL
            CONSTRAINT DF_ContactMessage_Status DEFAULT (N'new'),
        IsFlagged bit NOT NULL
            CONSTRAINT DF_ContactMessage_IsFlagged DEFAULT (0),
        CONSTRAINT CK_ContactMessage_Name
            CHECK (LEN(Name) >= 1 AND LEN(Name) <= 100),
        CONSTRAINT CK_ContactMessage_Email
            CHECK (
                LEN(Email) >= 3
                AND LEN(Email) <= 254
                AND Email LIKE N'%_@_%._%'
                AND Email NOT LIKE N'% %'
            ),
        CONSTRAINT CK_ContactMessage_Message
            CHECK (LEN(Message) >= 10 AND LEN(Message) <= 2000),
        CONSTRAINT CK_ContactMessage_Status
            CHECK (Status IN (N'new', N'read', N'archived', N'spam'))
    );
END;

/*
    Admin screens will list the newest messages, often filtered by status.
    One index serves both.
*/
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ContactMessage_Status_CreatedUtc'
      AND object_id = OBJECT_ID(N'dbo.ContactMessage')
)
BEGIN
    CREATE INDEX IX_ContactMessage_Status_CreatedUtc
        ON dbo.ContactMessage (Status, CreatedUtc DESC);
END;

/*
    ContactRateLimitHit is a log of accepted submissions, not of raw IPs.
    One row is written only when a message is accepted. A rejected attempt
    does not add a row, so a client who keeps retrying cannot push the
    window forward forever.

    IpHash + HitAtUtc answers "how many times has this hash called recently?"
    HitAtUtc alone answers "how many messages has the whole site accepted
    recently?" and supports the 48-hour cleanup.
*/
IF OBJECT_ID(N'dbo.ContactRateLimitHit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ContactRateLimitHit
    (
        Id bigint IDENTITY(1, 1) NOT NULL
            CONSTRAINT PK_ContactRateLimitHit PRIMARY KEY,
        IpHash binary(32) NOT NULL,
        HitAtUtc datetime2(3) NOT NULL
            CONSTRAINT DF_ContactRateLimitHit_HitAtUtc DEFAULT (SYSUTCDATETIME())
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ContactRateLimitHit_IpHash_HitAtUtc'
      AND object_id = OBJECT_ID(N'dbo.ContactRateLimitHit')
)
BEGIN
    CREATE INDEX IX_ContactRateLimitHit_IpHash_HitAtUtc
        ON dbo.ContactRateLimitHit (IpHash, HitAtUtc);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ContactRateLimitHit_HitAtUtc'
      AND object_id = OBJECT_ID(N'dbo.ContactRateLimitHit')
)
BEGIN
    CREATE INDEX IX_ContactRateLimitHit_HitAtUtc
        ON dbo.ContactRateLimitHit (HitAtUtc);
END;
