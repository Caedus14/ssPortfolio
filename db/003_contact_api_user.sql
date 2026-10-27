-- 003_contact_api_user.sql
-- ------------------------
-- Creates the login the Azure Function will use, and lets it run
-- usp_SubmitContact. It grants nothing else. The function cannot read
-- or edit the tables directly.
--
-- Run this third, connected to ssportfolio, not master.
--
-- Before the first run, replace REPLACE_WITH_A_STRONG_PASSWORD below.
-- Do not commit the real password. Generate one on your machine:
--
--     python3 -c "import secrets,string; chars=string.ascii_letters+string.digits+'!@#$%^&*-_'; print(''.join(secrets.choice(chars) for _ in range(40)))"
--
-- Azure SQL checks password complexity: at least 8 characters, from three
-- of uppercase, lowercase, digits, and symbols. This script asks for 20
-- characters and all four classes so the portal does not reject it later.
--
-- Running this file again does not change an existing password. To rotate
-- the password later, run this on its own, with a new password:
--
--     ALTER USER contact_api WITH PASSWORD = N'a-new-strong-password';
--
-- Azure SQL already allows contained users (a user that lives in the
-- database, with no server login). That is what Static Web Apps needs,
-- because the managed Function cannot use a managed identity against
-- SQL. Local SQL Server is different. See db/README.md.

IF DB_NAME() = N'master'
    THROW 51002, N'Connect to the ssportfolio database, not master, then run this script again.', 1;

IF OBJECT_ID(N'dbo.usp_SubmitContact', N'P') IS NULL
    OR OBJECT_ID(N'dbo.ContactMessage', N'U') IS NULL
    OR OBJECT_ID(N'dbo.ContactRateLimitHit', N'U') IS NULL
    THROW 51004, N'Run 001_tables.sql and 002_usp_SubmitContact.sql in ssportfolio first.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_principals
    WHERE name = N'contact_api'
)
BEGIN
    -- Edit only this line. Leave the checks below alone.
    DECLARE @Password nvarchar(128) = N'REPLACE_WITH_A_STRONG_PASSWORD';

    IF @Password LIKE N'%REPLACE_WITH_A_STRONG%'
        THROW 51000, N'The password still looks like the placeholder. Edit only the DECLARE line in 003_contact_api_user.sql. See db/README.md.', 1;

    IF LEN(@Password) < 20
        OR @Password COLLATE Latin1_General_BIN NOT LIKE N'%[a-z]%'
        OR @Password COLLATE Latin1_General_BIN NOT LIKE N'%[A-Z]%'
        OR @Password COLLATE Latin1_General_BIN NOT LIKE N'%[0-9]%'
        OR @Password COLLATE Latin1_General_BIN NOT LIKE N'%[^0-9A-Za-z]%'
        THROW 51001, N'Password must be at least 20 characters and include an uppercase letter, a lowercase letter, a digit, and a symbol.', 1;

    -- CREATE USER needs a string literal. Build that statement here so
    -- the password check above can run first. Quotes inside the password
    -- are doubled so they stay inside the string.
    DECLARE @CreateUser nvarchar(400) =
        N'CREATE USER contact_api WITH PASSWORD = N'''
        + REPLACE(@Password, N'''', N'''''')
        + N''';';

    EXEC sys.sp_executesql @CreateUser;
END;

-- EXECUTE is the only permission. The procedure and the tables are both
-- owned by dbo, so SQL Server's ownership chain lets the procedure write
-- the tables without granting contact_api access to the tables themselves.
--
-- Do not add contact_api to db_owner, db_datareader, or db_datawriter.
-- GRANT is safe to run more than once.
GRANT EXECUTE ON dbo.usp_SubmitContact TO contact_api;
