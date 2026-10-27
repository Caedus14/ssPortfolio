# Contact database scripts

These scripts create the Azure SQL objects for the contact form. The API that calls them is a later pull request. Nothing here changes the live React site.

The database is the free Azure SQL offer: serverless, auto-pause, server `sethstearns-sql`, database `ssportfolio`.

Run the scripts yourself in the Azure portal Query editor. They are plain T-SQL. They do not use SQLCMD commands such as `:setvar`, and they do not use `GO`. The portal runs the whole editor as one batch, and `GO` is only a separator for tools like SSMS and `sqlcmd`. `GO` in the portal fails with a syntax error.

The same editor rejects slash-star block comments. Pasting the old `001_tables.sql` failed with `Missing end comment mark '*/'. Failed to execute query.` The same statements run once those comments are removed. The IPv6 note in that file contained `/64` inside the block comment, and a `/` in that spot is enough for this editor's parser to lose the closing mark. These scripts use only `--` line comments. A `--` line can still mention a path such as `db/README.md` or the `/64` prefix. Keep new notes in that style.

## What each script does

Run them in this order, one file per Run click. Clear the editor between files so two files are never submitted together.

| Order | File                        | What it does                                                                                                                                                        |
| ----- | --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1     | `001_tables.sql`            | Creates `dbo.ContactMessage` and `dbo.ContactRateLimitHit`, their checks, and the indexes.                                                                          |
| 2     | `002_usp_SubmitContact.sql` | Creates or replaces `dbo.usp_SubmitContact`. This file is only the procedure, because SQL Server requires `CREATE PROCEDURE` to be the only statement in its batch. |
| 3     | `003_contact_api_user.sql`  | Creates the contained user `contact_api` and grants it `EXECUTE` on that procedure.                                                                                 |

Running a script again is safe. Tables and indexes are created only when they are missing. The procedure is replaced with `CREATE OR ALTER`. The user script does not reset a password that is already set.

These scripts create the first shape of the tables. If a table already exists, they do not add or change columns.

## How to run them in the portal

1. Open the Azure portal and go to the SQL database named `ssportfolio` on server `sethstearns-sql`.
2. Open **Query editor** and sign in as the SQL administrator or the Microsoft Entra administrator.
3. Confirm the database shown at the top is **ssportfolio**. If it says **master**, change it before you run anything. Script 001 and script 003 stop if they are connected to `master`. Script 002 cannot check that first (the procedure has to be the only statement), so check the name yourself. If you did run script 002 in `master`, run `DROP PROCEDURE IF EXISTS dbo.usp_SubmitContact;` there, then run script 002 again in `ssportfolio`.
4. Paste `001_tables.sql` and run it. Paste `002_usp_SubmitContact.sql` and run it.
5. Open `003_contact_api_user.sql` locally and change only the `DECLARE @Password` line. Replace the placeholder with a password you generate on your machine. Leave the checks under that line as they are. Do not commit that edit.

   ```bash
   python3 -c "import secrets,string; chars=string.ascii_letters+string.digits+'!@#$%^&*-_'; print(''.join(secrets.choice(chars) for _ in range(40)))"
   ```

   The password must be at least 20 characters and include an uppercase letter, a lowercase letter, a digit, and a symbol. Azure SQL rejects weaker passwords. The script also refuses to run while the placeholder is still there.

6. Paste the edited script 003 into the query editor and run it. Store the password in the Function's settings later, not in git.
7. To change the password later, run this on its own. Re-running script 003 will not rotate it.

   ```sql
   ALTER USER contact_api WITH PASSWORD = N'a-new-strong-password';
   ```

The first query after the free database auto-pauses can take a minute while the database resumes. That wait happens before the procedure runs. The procedure's own lock wait is 5 seconds and is unrelated to resume.

## What the tables store

`dbo.ContactMessage` is the inbox.

| Column       | Meaning                                                                                                               |
| ------------ | --------------------------------------------------------------------------------------------------------------------- |
| `Name`       | 1 to 100 characters.                                                                                                  |
| `Email`      | Up to 254 characters, with a rough shape check (`something@somewhere.tld`). This is not a full email-standard parser. |
| `Message`    | 10 to 2000 characters.                                                                                                |
| `CreatedUtc` | When the row was accepted, in UTC.                                                                                    |
| `IpHash`     | 32 raw bytes, HMAC-SHA256. Never a plain IP address.                                                                  |
| `Status`     | `new`, `read`, `archived`, or `spam`. New rows are `new`. A later admin screen can change this.                       |
| `IsFlagged`  | `0` or `1`. New rows are `0`.                                                                                         |

`dbo.ContactRateLimitHit` is one row per **accepted** submission: the same 32-byte hash and a UTC time. Rejected attempts do not add a row. If they did, a client who kept retrying would slide the window forward and never reach the retry time.

The hash is computed in the API, with a secret key that is not stored in SQL:

- IPv4: HMAC of the canonical dotted address, for example `203.0.113.8`.
- IPv6: group the address by `/64` first, then HMAC the four lowercase hextets of that prefix, for example `2001:db8:abcd:12`.
- Send the raw 32-byte HMAC. Do not send a hex string. In the query editor a 32-byte value is written as `0x` plus 64 hex digits.

The hash is not reversible without the key. The key still has to stay secret, because anyone with the key can test an IP address against the stored hashes. The name, email, and message are stored in plain text so you can reply. Treat the database as personal data.

## What the procedure does

`dbo.usp_SubmitContact` is the only write path.

1. Trim the name, email, and message. If a length or the email shape is wrong, or the hash is not exactly 32 bytes, return `invalid` and write nothing.
2. Take one exclusive application lock named `usp_SubmitContact`. Two calls at once wait their turn, so they cannot both read "49 today" and both insert. If the lock is not available within 5 seconds, return `busy`.
3. Delete hit rows older than 48 hours. That window is longer than the 24-hour limit, so the delete cannot change the current decision. It keeps the table small.
4. Count this hash's hits in the last 10 minutes, this hash's hits in the last 24 hours, and every hash's hits in the last 24 hours.
5. If the hash is at 3 in 10 minutes, or 10 in 24 hours, return `rate_limited_ip`. If the site is at 50 in 24 hours, return `rate_limited_global`. A rejected call does not insert a hit. The cleanup from step 3 is kept.
6. Otherwise insert one hit and one message (`Status = new`, `IsFlagged = 0`) and return `accepted`.

The limits are parameter defaults, so the numbers are easy to find at the top of the procedure. Callers can omit them.

Check order is burst, then daily, then global. The first limit that fails is the one returned.

`RetryAfterSeconds` is how long until the oldest hit that is occupying a slot falls out of the window. With the usual limits, that is the oldest of the 3 hits in 10 minutes, the oldest of the 10 hits in 24 hours, or the oldest hit that still keeps the site at 50.

Every result row has the same columns:

| `ResultCode` | `ResultName`          | `Reason`                 | `RetryAfterSeconds`        | `MessageId` |
| ------------ | --------------------- | ------------------------ | -------------------------- | ----------- |
| 0            | `accepted`            | null                     | null                       | new id      |
| 1            | `rate_limited_ip`     | `ip_burst` or `ip_daily` | seconds until a slot opens | null        |
| 2            | `rate_limited_global` | `global_cap`             | seconds until a slot opens | null        |
| 3            | `invalid`             | which check failed       | null                       | null        |
| 5            | `busy`                | `lock_timeout`           | 5                          | null        |

The procedure also `RETURN`s the same number as `ResultCode`.

## How to verify

Stay signed in as the admin. This sample hash is 32 bytes. It is not a real visitor.

```sql
DECLARE @Hash binary(32) = 0x1111111111111111111111111111111111111111111111111111111111111111;

EXEC dbo.usp_SubmitContact
    @Name = N'Test Visitor',
    @Email = N'test@example.com',
    @Message = N'This is a test message.',
    @IpHash = @Hash;
```

Run that three times. Each result is `ResultCode` 0 and a new `MessageId`. The fourth run returns `ResultCode` 1, `Reason` `ip_burst`, and a `RetryAfterSeconds` value of about 600 (10 minutes).

A message of `short` returns `ResultCode` 3.

Then prove `contact_api` cannot read the inbox. This borrows that user's permissions for the next statement only:

```sql
EXECUTE AS USER = N'contact_api';
SELECT TOP (1) Id FROM dbo.ContactMessage;
REVERT;
```

The `SELECT` fails with a permission error. `REVERT` still runs if you execute the three lines together and the portal stops at the error, so if the session is stuck as `contact_api`, run `REVERT;` on its own before anything else.

This call should succeed and then return `ip_burst`, because the three test rows above are still inside the window:

```sql
EXECUTE AS USER = N'contact_api';
EXEC dbo.usp_SubmitContact
    @Name = N'Test Visitor',
    @Email = N'test@example.com',
    @Message = N'Another test from the API user.',
    @IpHash = 0x1111111111111111111111111111111111111111111111111111111111111111;
REVERT;
```

## How to clean up the test rows

```sql
DECLARE @Hash binary(32) = 0x1111111111111111111111111111111111111111111111111111111111111111;

DELETE FROM dbo.ContactMessage WHERE Email = N'test@example.com';
DELETE FROM dbo.ContactRateLimitHit WHERE IpHash = @Hash;
```

Do this as the admin. `contact_api` cannot delete rows.

## Local SQL Server is not Azure SQL

On Azure SQL, `CREATE USER ... WITH PASSWORD` works as soon as you are in `ssportfolio`. Contained users are already enabled.

On a local SQL Server you have to turn that feature on once, as an administrator, before script 003:

```sql
EXEC sys.sp_configure N'contained database authentication', 1;
RECONFIGURE;
ALTER DATABASE ssportfolio SET CONTAINMENT = PARTIAL;
```

Do not add those two statements to script 003. Azure SQL rejects the configuration change, and the database there is already contained.

`EXECUTE AS OWNER` is not used. The procedure runs as the caller. That caller can write the tables only through the procedure, because the procedure and the tables share the dbo owner (ownership chaining). Granting `EXECUTE` does not grant `SELECT`, `INSERT`, `UPDATE`, or `DELETE`.
