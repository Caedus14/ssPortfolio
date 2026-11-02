# Contact API

This folder is the Azure Functions app for the contact form. `GET /api/health` reports that the process is up. `GET /api/contact/token` mints a signed timestamp. `POST /api/contact` runs the cheap spam checks below.

A request that passes every check is stored by `dbo.usp_SubmitContact` and returns HTTP 200 `{ "status": "accepted" }`. The same JSON is returned for a honeypot hit, and that hit is not stored. The React site does not show a form yet. `features.contactApi` in `src/config/site.ts` stays `false` until a later change wires the form to this contract. The public site key for that form is `turnstileSiteKey` in the same file.

## Why it is shaped this way

The site is an Azure Static Web Apps Free app. On that plan the API is a **managed function**: Static Web Apps builds and hosts the Functions app for you. That comes with limits that decided the project shape:

- HTTP triggers only. A timer, a queue, or a Durable Functions workflow is not available here.
- No managed identity. The function signs in to SQL with the `contact_api` user and a connection string. It cannot use `DefaultAzureCredential`.
- A request is limited to 45 seconds. `host.json` sets `functionTimeout` to that same value so a local hang fails the way production will.
- Routes live under `/api`. The function route is `health`, `contact`, or `contact/token`. The host adds `/api`.

.NET 10 isolated is the runtime. Static Web Apps lists it as `dotnet-isolated:10.0` in `public/staticwebapp.config.json` (`platform.apiRuntime`). Support for the .NET 8 and .NET 9 isolated runtimes ends on 10 November 2026, which is why this project does not target those. .NET 10 on this host is isolated-worker only. The in-process model is not available for .NET 10.

"Isolated" means the function code runs in its own process, next to the Functions host. `Program.cs` is that process. `ConfigureFunctionsWebApplication()` lets a function use ASP.NET Core types (`HttpRequest`, `IActionResult`). It does not give you the full ASP.NET Core middleware pipeline, and it does not change the Static Web Apps limits above.

## Contract

JSON property names are camelCase. The Functions host writes them that way, the same as `GET /api/health` (`status`, `service`). Clients may send either `name` or `Name`. A property that is present must be a JSON string. A number or an object is `invalid_json`. Unknown properties are ignored, so a later client can send an extra field without a coordinated deploy.

### `GET /api/health`

200

```json
{ "status": "ok", "service": "ssportfolio-api" }
```

### `GET /api/contact/token`

Call this when the form opens. Send the returned `formToken` back on submit.

200, when `FORM_TOKEN_KEY` is set and at least 16 characters:

```json
{ "formToken": "MTcwMDAwMDAwMDAwMA.8v3h...base64url-hmac" }
```

503, when the key is missing, blank, or shorter than 16 characters:

```json
{ "error": "form_token_unavailable" }
```

The process stays up. `/api/health` still returns 200. The log line is an error and names `FORM_TOKEN_KEY`.

The token is `base64url(utf8(unix-milliseconds))` + `.` + `base64url(hmac-sha256)`. The signature covers the first part exactly as it appears. The key never leaves the server, so a client cannot mint an old timestamp. Comparison of the signature uses a constant-time check after the lengths match.

### `POST /api/contact`

```json
{
  "name": "Ada Lovelace",
  "email": "ada@example.com",
  "message": "Hello. I would like to talk about a role.",
  "website": "",
  "formToken": "<the formToken from GET /api/contact/token>",
  "turnstileToken": ""
}
```

| Field            | Meaning                                                                                                                                     |
| ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `name`           | Trimmed length 1 to 100. No control characters.                                                                                             |
| `email`          | Trimmed. 3 to 254 characters. One `@`, not at either end. The domain has a dot that is not its first or last character, and it has no `..`. |
| `message`        | Trimmed length 10 to 2000. Newlines and tabs are allowed. Other control characters are not. At most 2 links. No HTML tags.                  |
| `website`        | Honeypot. The real form renders this input and leaves it empty. A person never fills it.                                                    |
| `formToken`      | The token from `GET /api/contact/token`. Required once the honeypot is empty.                                                               |
| `turnstileToken` | Token from the Turnstile widget. Required after the other fields pass. Maximum 2048 characters. The widget action must be `contact`.        |

`email` keeps the caller's casing. The check is the SQL shape (`something@somewhere.tld`, no spaces) without a long regular expression.

A link is `http://`, `https://`, or `www.`, case-insensitive. The counter then skips to the next whitespace, so `https://www.example.com` is one link. An HTML tag is `<` immediately followed by a letter, `</` followed by a letter, `<!`, or `<?`. `a < b` and `5 < 10` are allowed. A space after `<` is an evasion this check misses.

The body must be UTF-8. `Content-Type` must be `application/json`, optionally with a `charset` parameter. `charset` is not trusted: the bytes are always read as UTF-8. The raw body cap is 8192 bytes. A `Content-Length` over that cap is rejected before the body is read. A missing or too-small `Content-Length` still stops at 8193 bytes. A 2000-character message made of 4-byte characters can exceed 8192 bytes and is rejected here, before the length check runs.

Checks run in this order. The first failure wins, except field errors, which are all returned together:

1. Content type.
2. Body size.
3. JSON shape.
4. Honeypot.
5. Form token.
6. Field rules.
7. IP hash. `IP_HASH_KEY` must be at least 16 characters. A missing or short key is 503 `unavailable` and siteverify is not called. When no client IP can be named, every such request shares the label `unknown`.
8. In-memory rate limit, keyed by that hash. Defaults are 5 attempts per minute and 20 per hour. A refusal is 429 and siteverify is not called.
9. Turnstile siteverify.
10. `dbo.usp_SubmitContact` through `SQL_CONNECTION_STRING`.

| Status | Body                                                                                                                                          | When                                                                                                                            |
| ------ | --------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| 415    | `{ "error": "unsupported_media_type" }`                                                                                                       | Content type is missing or is not `application/json`.                                                                           |
| 413    | `{ "error": "payload_too_large" }`                                                                                                            | Body is larger than 8192 bytes.                                                                                                 |
| 400    | `{ "error": "invalid_json" }`                                                                                                                 | The body is not a JSON object, a known field is not a string, or the bytes are not UTF-8.                                       |
| 200    | `{ "status": "accepted" }`                                                                                                                    | The honeypot was filled, or the procedure stored the message. The JSON is the same. A honeypot is not stored.                   |
| 429    | `{ "error": "rate_limited", "retryAfterSeconds": N }`                                                                                         | The in-memory window or the procedure's per-IP or global cap. `Retry-After` is the same number of seconds.                      |
| 503    | `{ "error": "unavailable", "retryAfterSeconds": N }`                                                                                          | `IP_HASH_KEY` or `SQL_CONNECTION_STRING` is missing, the database is still waking after retries, or the procedure is busy.      |
| 400    | `{ "error": "form_token_required", "fields": { "formToken": "Reload the page and try again." } }`                                             | `formToken` is missing or blank. This does not need the signing key.                                                            |
| 400    | `{ "error": "form_token_invalid", "fields": { "formToken": "Reload the page and try again." } }`                                              | Tampered, forged, wrong shape, or more than 30 seconds in the future.                                                           |
| 400    | `{ "error": "form_token_expired", "fields": { "formToken": "This form has been open too long. Reload the page and try again." } }`            | Older than 2 hours.                                                                                                             |
| 400    | `{ "error": "form_token_too_fast", "fields": { "formToken": "Please wait a few seconds and try again." } }`                                   | Younger than 3 seconds, including a timestamp up to 30 seconds in the future.                                                   |
| 503    | `{ "error": "form_token_unavailable" }`                                                                                                       | A token was sent, but `FORM_TOKEN_KEY` is missing or shorter than 16 characters.                                                |
| 400    | `{ "error": "invalid", "fields": { "name": "...", "email": "...", "message": "..." } }`                                                       | One or more fields failed. Only the failing fields are present.                                                                 |
| 400    | `{ "error": "turnstile_required", "fields": { "turnstileToken": "Complete the check and try again." } }`                                      | `turnstileToken` is missing or blank. This does not need the secret and does not call Cloudflare.                               |
| 400    | `{ "error": "turnstile_timeout", "fields": { "turnstileToken": "The check expired or was already used. Complete it again and try again." } }` | Cloudflare returned `timeout-or-duplicate`, or `challenge_ts` is older than 300 seconds.                                        |
| 400    | `{ "error": "turnstile_failed", "fields": { "turnstileToken": "Complete the check again and try again." } }`                                  | The token, hostname, or action was rejected. A token longer than 2048 characters is rejected here too, with no siteverify call. |
| 503    | `{ "error": "turnstile_unavailable" }`                                                                                                        | `TURNSTILE_SECRET` is missing, Cloudflare rejected the secret, or siteverify could not be completed.                            |

`fields` is omitted when the response has no per-field message. There is no `messageId` on the honeypot's 200. Adding one would let a bot tell a trap from a real accept.

Field messages, for the form to show next to the input:

| `fields` key     | Message                                                         |
| ---------------- | --------------------------------------------------------------- |
| `name`           | `Name must be 1 to 100 characters.`                             |
| `name`           | `Name cannot include control characters.`                       |
| `email`          | `Enter an email address, up to 254 characters, without spaces.` |
| `message`        | `Message must be 10 to 2000 characters.`                        |
| `message`        | `Message cannot include control characters.`                    |
| `message`        | `Message cannot include HTML.`                                  |
| `message`        | `Message can include at most 2 links.`                          |
| `formToken`      | One of the three sentences in the status table above.           |
| `turnstileToken` | One of the three sentences in the status table above.           |

The first matching rule wins inside a single field. A short message that also contains HTML reports the length. A message that contains both HTML and three links reports the HTML.

## Why each layer

These are ordered cheapest first. A request that is huge, or not JSON, should die before any cryptography or string scanning.

**Size and content type.** A managed function has a short timeout and a small instance. Reading an unbounded body is a cheap way to waste that. 8 KiB is enough for a normal note and small enough to refuse a stream. Requiring `application/json` means a browser form post and a stray text body fail the same way, before the parser runs.

**Honeypot.** The form will include a `website` input that CSS hides from people. Bots often fill every input. When `website` has any non-whitespace text, the API returns the same 200 as a future real accept and stops. It does not check the token, validate the other fields, or store anything. The log line is debug level and does not include what was typed.

An error would teach the bot to leave `website` blank and try again. A person never fills the hidden field, so a person never sees this response. Returning success gives the bot nothing to correct.

**Signed minimum time.** A token is minted when the form loads. Submit is refused until that token is at least 3 seconds old, and a token older than 2 hours is refused too. The timestamp is signed with `FORM_TOKEN_KEY`. A bot that invents `issuedAt = now - 10 seconds` cannot produce a matching HMAC.

`FORM_TOKEN_KEY` is its own setting. `IP_HASH_KEY` is the other HMAC key, and it can test an IP address against a hash the database already stores. The token key only proves when a form was opened. One leaked secret should not hand over the other job. They also rotate on different days: changing the IP key makes old hashes unsearchable, while changing the form key only asks people with an open tab to reload.

Too fast, expired, and invalid tokens are HTTP 400 with a sentence on `fields.formToken`. A person can submit in under 3 seconds, or leave a tab open through lunch. They need a message that says wait or reload. A fake success here would tell that person the note was sent. A bot still cannot forge the timestamp without the key. A clock that is up to 30 seconds ahead looks like "too fast", which is the sentence that fits. Further ahead looks invalid.

The same token can be submitted again until it expires. Remembering used tokens would need storage shared by every instance, and this plan has none. A later step adds an in-memory per-IP limit and the SQL limits already in [`db/README.md`](../db/README.md): 3 per 10 minutes and 10 per 24 hours for one IP hash, and 50 per 24 hours for everyone. Those caps are what stop a copied token from being replayed in volume.

If the key is missing, issuing a token and checking a token both return 503. They do not throw, and they do not skip the check. A preview environment without the setting shows this 503. A missing token is still 400, because that decision does not need the key. A honeypot hit is still 200 for the same reason.

**Field rules.** The database procedure already rejects a name, email, or message outside those lengths. Doing it here returns one message per field, which the form can put on the matching input. The link cap and the HTML check are extra. They are the spam that still looks like a valid note. The procedure repeats the length checks and can still return `invalid` if they ever disagree. That response is 400 `{ "error": "invalid" }` with no `fields` object.

**IP hash.** The address from the header rules below is normalized, then HMAC-SHA256'd with `IP_HASH_KEY`. The database stores the raw 32 bytes, never the address. IPv4 is the dotted form (`203.0.113.8`). IPv6 is the `/64` prefix as four lowercase hextets (`2001:db8:abcd:12`). An IPv4-mapped IPv6 address is hashed as IPv4. The format is written out in [`db/README.md`](../db/README.md). A missing or short key is 503 `unavailable` with `Retry-After: 15`. The process does not throw. A request with no client IP hashes the label `unknown`, so those requests share one bucket instead of skipping the limit.

**In-memory rate limit.** This is a sliding window in the current process: 5 attempts per minute and 20 per hour for one hash, overridable with `CONTACT_MEMORY_PER_MINUTE`, `CONTACT_MEMORY_PER_HOUR`, and `CONTACT_MEMORY_MAX_ENTRIES` (default 4096 hashes). Every attempt that reaches this step is counted, including the one that is refused. The map drops hashes that have been idle for an hour, and it drops the least recently seen hash when the cap is full, so a flood cannot grow the dictionary without bound. Each hash keeps at most 64 timestamps. The response is 429 with `Retry-After` and `retryAfterSeconds`. This limit is per instance and it is empty after a cold start. It exists so a burst does not call siteverify. The procedure is the limit that every instance shares.

**Turnstile.** The widget in the browser produces a token. That token is not proof. The server posts it to `https://challenges.cloudflare.com/turnstile/v0/siteverify` with `TURNSTILE_SECRET`, the token, the visitor IP when one can be named safely, and a fresh `idempotency_key` (a UUID). Cloudflare answers with `success`, `hostname`, `action`, and `challenge_ts`.

The end-to-end path the form will use:

1. The page loads the public site key from `turnstileSiteKey` in `src/config/site.ts` (`0x4AAAAAAForbSDVPuig8KiS`). The secret never goes to the browser.
2. The widget is rendered in Managed mode with action `contact` (`turnstileAction` in that file).
3. On submit, the page sends `turnstileToken` with the other fields.
4. This API calls siteverify. `HttpClient` gives up after 8 seconds.
5. A passing token must name an allowed hostname, carry action `contact`, and have `challenge_ts` within 300 seconds (30 seconds of future clock skew is allowed). Tokens are single-use. A second submit of the same token comes back as `timeout-or-duplicate`.

Allowed hostnames are `sethstearns.dev`, `www.sethstearns.dev`, `nice-rock-033c92210.2.azurestaticapps.net`, and this app's preview hosts (`nice-rock-033c92210-<pull request>.<region>.2.azurestaticapps.net`). `TURNSTILE_ALLOWED_HOSTS` adds names. It does not remove the built-in ones. The widget in Cloudflare also has its own hostname list. A preview host has to be on both lists before a real token from that host will pass.

`remoteip` is optional for Cloudflare. This API sends it only when the address is one a proxy we trust recorded.

- `X-Forwarded-For` can be set by the browser. The leftmost value is the attacker's if a proxy appends instead of replacing. This API does not use the leftmost value of a list.
- On a managed function the chain looks like `visitor, azure-edge`. The rightmost address is the last proxy. The visitor that proxy saw is the address just to its left. A single value is used as-is.
- `client-ip` on these functions is a private hop inside Azure, not the visitor. It is ignored.
- `X-Azure-ClientIP`, when the edge sets it, wins. Azure Front Door overwrites that header, so a value the browser sent does not survive. Captures of managed functions often omit it.
- When none of those yield an address, `remoteip` is left out of the siteverify call. The token is still checked. The rate limiter and SQL still run, using the shared `unknown` hash. A wrong address would be worse than none on the Cloudflare call.

The idempotency key lets a retry of the same siteverify call reuse Cloudflare's first answer. This step does not retry on its own. A second click in the browser is a new call with a new key, so a spent token still fails.

Failures:

- A missing token is 400 `turnstile_required`. That does not need the secret, so a preview without `TURNSTILE_SECRET` can still show it. A honeypot hit still returns 200 before this check.
- `timeout-or-duplicate`, or a `challenge_ts` older than 300 seconds, is 400 `turnstile_timeout`. The person can complete the widget again. A fake success would tell them the note was sent.
- Any other rejected token, hostname, or action is 400 `turnstile_failed`. The log names the hostname and the error code. It does not include the token or the secret.
- A missing secret, `invalid-input-secret`, `missing-input-secret`, `internal-error`, `bad-request`, an HTTP error, or a timeout talking to Cloudflare is 503 `turnstile_unavailable`. The process stays up. The check is not skipped. Accepting the post because Cloudflare was down would turn an outage into an open form. The person can try again when the call works. `invalid-input-secret` is logged as a configuration problem.

Cloudflare's published test secret that always passes returns `hostname: example.com`, a fresh `challenge_ts`, and no `action`, plus `metadata.result_with_testing_key: true`. That one hostname is allowed for a testing-key response, and a missing action is allowed only then. A production secret does not get that exception. `example.com` on a real secret is rejected.

**SQL.** After Turnstile passes, `SqlContactSubmitter` calls `dbo.usp_SubmitContact` as a stored procedure with `@Name`, `@Email`, `@Message`, and `@IpHash`. The login is `contact_api`, which can only `EXECUTE` that procedure. The result row is mapped like this:

| `ResultCode`            | HTTP | Body                                                                                           |
| ----------------------- | ---- | ---------------------------------------------------------------------------------------------- |
| 0 `accepted`            | 200  | `{ "status": "accepted" }`                                                                     |
| 1 `rate_limited_ip`     | 429  | `{ "error": "rate_limited", "retryAfterSeconds": N }` from the procedure's `RetryAfterSeconds` |
| 2 `rate_limited_global` | 429  | the same body, with that procedure value as `Retry-After`                                      |
| 3 `invalid`             | 400  | `{ "error": "invalid" }`                                                                       |
| 5 `busy`                | 503  | `{ "error": "unavailable", "retryAfterSeconds": N }`                                           |

A missing connection string is the same 503 with `retryAfterSeconds` 15. So is a SQL error that is not in the wake-up set, and so is giving up after retries.

The free database auto-pauses. The first call then fails with `40613` (or `4060`, `40197`, `40501`, `49918`, `49919`, `49920`) or a connection timeout (`-2`). Those are retried with waits of 250 ms, 1 s, and 2 s. Connect time and command time are each capped at 5 seconds. New attempts stop after 20 seconds, which is under the 45-second function limit. The response the form will show is 503 with `Retry-After: 15`, so the person can try again in a moment. Logs record the SQL error number. They do not record the connection string, the email, or the message.

The accepted JSON has no `messageId`. The honeypot uses that same body, and a message id would let a bot tell them apart. The id is written to the log.

## Layout

```
api/
  Portfolio.Api.csproj          .NET 10 isolated worker
  Program.cs                    Host startup and dependency registration
  host.json                     45-second timeout, shared by every function
  local.settings.json.example   Empty settings. Copy this; do not commit the copy.
  Functions/                    HTTP edge. Health, token, and contact.
  Contact/                      The checks, one small type each, then IContactSubmitter
  Options/                      App setting names, including TURNSTILE_SECRET
api.tests/                      xUnit tests. Not deployed.
```

`ContactPipeline` is the order of checks. `FormTokenCodec` signs the form token. `HmacIpAddressHasher` is the IP HMAC. `TimeProvider` is the clock, so a test can move time without sleeping. The database stores 32 bytes of HMAC, never the visitor's IP address.

## How a request moves

`GET /api/health`

1. Static Web Apps receives the request and forwards `/api/*` to the function app.
2. The host matches the route `health` on `HealthFunction`.
3. The function returns 200 and `{ "status": "ok", "service": "ssportfolio-api" }`.

`GET /api/contact/token`

1. `ContactTokenFunction` asks `IFormTokenService` for a signed timestamp.
2. With a usable `FORM_TOKEN_KEY`, the response is 200 and `{ "formToken": "..." }`.
3. Without one, the response is 503 and `{ "error": "form_token_unavailable" }`.

`POST /api/contact`

1. `ContactFunction` hands the request to `ContactPipeline`.
2. The pipeline runs the checks in the contract table.
3. A honeypot hit returns 200 and `{ "status": "accepted" }`.
4. Anything else that fails returns the 400, 413, 415, or 503 from that table.
5. The IP is hashed. A missing `IP_HASH_KEY` returns 503 `unavailable`.
6. The in-memory rate limit runs. A hit returns 429 and does not call Cloudflare.
7. Turnstile siteverify runs when `turnstileToken` is present and `TURNSTILE_SECRET` is set. Otherwise the response is the 400 or 503 from the contract table.
8. A request that passes calls `dbo.usp_SubmitContact`.
9. The response is 200 `{ "status": "accepted" }` when the procedure stores the row. The other procedure codes are in the table above.

Every function uses `AuthorizationLevel.Anonymous`. A function key would block the browser. Static Web Apps is the front door, and on the Free plan the managed function is not reachable as its own public Function App.

## Settings

Copy the example before you run the host:

```bash
cp api/local.settings.json.example api/local.settings.json
```

`local.settings.json` is gitignored. The example is safe to commit because every secret is an empty string.

| Name                         | What it is                                                                                                                                              |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `FUNCTIONS_WORKER_RUNTIME`   | `dotnet-isolated`. Tells the host which worker to start.                                                                                                |
| `AzureWebJobsStorage`        | Empty locally. HTTP-only functions do not need a storage account for these routes.                                                                      |
| `SQL_CONNECTION_STRING`      | ADO.NET string for database `ssportfolio` on server `sethstearns-sql`, user `contact_api`. Empty until you set it. Connect time is capped at 5 seconds. |
| `TURNSTILE_SECRET`           | Cloudflare Turnstile secret for siteverify. Empty until you set it. The public site key is `turnstileSiteKey` in `src/config/site.ts`.                  |
| `TURNSTILE_ALLOWED_HOSTS`    | Optional comma-separated hostnames added to the built-in allow list. Empty uses only the built-in names.                                                |
| `TURNSTILE_EXPECTED_ACTION`  | Optional. Empty means `contact`.                                                                                                                        |
| `IP_HASH_KEY`                | HMAC key for the visitor IP. At least 16 characters. Anyone with this key can test addresses against the stored hashes.                                 |
| `FORM_TOKEN_KEY`             | Secret key for form tokens only. At least 16 characters. Generate about 40. The value is used exactly as stored, including a trailing space.            |
| `CONTACT_MEMORY_PER_MINUTE`  | Optional. Empty uses 5.                                                                                                                                 |
| `CONTACT_MEMORY_PER_HOUR`    | Optional. Empty uses 20.                                                                                                                                |
| `CONTACT_MEMORY_MAX_ENTRIES` | Optional. Empty uses 4096.                                                                                                                              |

In Azure, set the secrets under the Static Web App's **Environment variables** (also shown as Configuration / Application settings). Do not put them in `staticwebapp.config.json`. That file is in the repo and is public. The Functions host reads the same names from the environment, which is what `ContactApiOptionsBinder` looks up.

`FORM_TOKEN_KEY`, `TURNSTILE_SECRET`, `IP_HASH_KEY`, and `SQL_CONNECTION_STRING` are the settings this API reads. Add them in the portal when the later deploy step says to. Until `FORM_TOKEN_KEY` is there, `GET /api/contact/token` returns 503, and a POST that includes `formToken` returns 503 `form_token_unavailable`. Until `TURNSTILE_SECRET` is there, a POST that gets past the form token returns 503 `turnstile_unavailable`. Until `IP_HASH_KEY` is there, a POST that gets past the form token returns 503 `unavailable`. On this preview the form-token check runs first, so that is the 503 you will see. Health, a honeypot POST, and a POST with no form token keep working. Do not commit a real key. An empty string in the example is the committed value.

Do not put Cloudflare's always-pass test secret in Azure. It accepts every dummy token. Paste it only into the gitignored `api/local.settings.json` when you want to call siteverify from your machine. The committed example stays empty so a copy of that file into the portal does not open the form.

Generating a password or a hash key:

```bash
python3 -c "import secrets,string; chars=string.ascii_letters+string.digits+'!@#$%^&*-_'; print(''.join(secrets.choice(chars) for _ in range(40)))"
```

The SQL user itself is created by `db/003_contact_api_user.sql`. Put that user's connection string in `SQL_CONNECTION_STRING`. Do not put it in this file.

## Run it locally

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download), [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local), and Node.js 22 (already used by the site).

From the repo root, with Core Tools on your `PATH`:

```bash
cp api/local.settings.json.example api/local.settings.json
cd api
func start
```

Put a generated `FORM_TOKEN_KEY` in the copied `local.settings.json` when you want the token endpoint to return 200. Leave it empty to see the 503.

For Turnstile on your machine, set `TURNSTILE_SECRET` in that same gitignored file to Cloudflare's always-pass test secret:

```text
1x0000000000000000000000000000000AA
```

The matching dummy token is `XXXX.DUMMY.TOKEN.XXXX`. Siteverify then returns success, hostname `example.com`, and no action. This API allows that shape only when Cloudflare marks the response `result_with_testing_key`. The other published test secrets are `2x0000000000000000000000000000000AA` (always fail, `invalid-input-response`) and `3x0000000000000000000000000000000AA` (token already spent, `timeout-or-duplicate`). The published test site keys, for a later local widget, are `1x00000000000000000000AA` (always pass), `2x00000000000000000000AB` (always block), and `3x00000000000000000000FF` (force an interactive challenge). A production secret rejects the dummy token. Leave `TURNSTILE_SECRET` empty to see the 503.

`dotnet run --project api` does the same thing when Core Tools is installed. The SDK starts the Functions host for you.

The host listens on `http://localhost:7071`.

```bash
curl -sS http://127.0.0.1:7071/api/health
curl -sS -D- http://127.0.0.1:7071/api/contact/token
curl -sS -D- -H 'content-type: application/json' \
  -d '{"website":"bot"}' \
  http://127.0.0.1:7071/api/contact
curl -sS -D- -H 'content-type: application/json' \
  -d '{"name":"Ada"}' \
  http://127.0.0.1:7071/api/contact
```

Health is 200. The token call is 503 until the key is set, then 200. The honeypot body is 200 and `{ "status": "accepted" }`. The body with no token is 400 and `form_token_required`.

With `AzureWebJobsStorage` left empty, the host log says the storage health check is unhealthy. That check is for queues and blobs, which these routes do not use. The HTTP routes still answer. Do not set `AzureWebJobsStorage` in the Static Web App. The platform fills that setting itself.

To click around the site and call the API as the same origin, start the Functions host, then in another terminal:

```bash
npm run dev
```

Vite proxies `/api` to port 7071, so `http://localhost:5173/api/health` hits the function. That proxy exists only in `npm run dev`. `npm run build` does not include it. Production uses Static Web Apps routing instead.

The [Static Web Apps CLI](https://azure.github.io/static-web-apps-cli/) is the closer match to Azure, because it applies `staticwebapp.config.json` locally. With the Functions host already running and `npm run dev` already running:

```bash
npx @azure/static-web-apps-cli start http://localhost:5173 --api-devserver-url http://127.0.0.1:7071
```

The CLI listens on port 4280 by default.

## Tests

From the repo root:

```bash
dotnet build ssportfolio.sln
dotnet test ssportfolio.sln
```

The tests call the function methods in process. They do not start the Functions host and they do not need a real secret. `TimeProvider` is a fixed clock. GitHub Actions runs the same two commands in the "Build and test the API" job, next to the existing frontend job.

Coverage includes the content-type and 8 KiB boundaries, the honeypot's fake 200, tokens that are too fast, expired, tampered, forged, or signed with a short key, link counting, HTML detection, Turnstile's pass, fail, and already-spent shapes against a mocked siteverify call, the in-memory window and its eviction cap, the IP hash vectors, every procedure result code, and SQL retries against a fake procedure. The unit tests do not call Cloudflare or Azure SQL. A request that passes the checks returns 200 from the submitter stand-in.
