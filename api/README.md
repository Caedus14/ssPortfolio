# Contact API

This folder is the Azure Functions app for the contact form. `GET /api/health` reports that the process is up. `GET /api/contact/token` mints a signed timestamp. `POST /api/contact` runs the cheap spam checks below.

A request that passes every check still returns HTTP 501. Nothing is stored. The in-memory rate limit and the SQL call are later steps. Turnstile is checked. The React site does not show a form yet. `features.contactApi` in `src/config/site.ts` stays `false` until a later change wires the form to this contract. The public site key for that form is `turnstileSiteKey` in the same file.

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
7. In-memory rate limit. The registered class allows every request. Step 7 replaces that class. The call stays here, before siteverify, so a flood does not spend Cloudflare requests.
8. Turnstile siteverify.
9. `IContactSubmitter`. Today that is the 501 placeholder. It does not store the message.

| Status | Body                                                                                                                                          | When                                                                                                                            |
| ------ | --------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| 415    | `{ "error": "unsupported_media_type" }`                                                                                                       | Content type is missing or is not `application/json`.                                                                           |
| 413    | `{ "error": "payload_too_large" }`                                                                                                            | Body is larger than 8192 bytes.                                                                                                 |
| 400    | `{ "error": "invalid_json" }`                                                                                                                 | The body is not a JSON object, a known field is not a string, or the bytes are not UTF-8.                                       |
| 200    | `{ "status": "accepted" }`                                                                                                                    | `website` is not blank after trim. Same shape a real accept will use later. Nothing is stored.                                  |
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
| 501    | `{ "error": "not_implemented" }`                                                                                                              | Every check passed, including Turnstile. Placeholder until the rate limit and SQL exist.                                        |

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

**Field rules.** The database procedure already rejects a name, email, or message outside those lengths. Doing it here returns one message per field, which the form can put on the matching input. The link cap and the HTML check are extra. They are the spam that still looks like a valid note. The procedure's result codes stay in `ContactProcedureCodes` for the later SQL step. This step does not call the procedure.

**Rate limit slot.** `IContactRateLimiter` runs next. `PassThroughContactRateLimiter` allows every request. Step 7 replaces that registration. It stays in front of Turnstile on purpose. Siteverify is a call to Cloudflare, and a flood of otherwise valid posts would burn that quota if the limit ran afterwards. The SQL limits in [`db/README.md`](../db/README.md) still apply when the procedure exists. They are a second layer, after a message is actually stored. An `Origin` check, when it is added, belongs with these cheap checks too.

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
- When none of those yield an address, `remoteip` is left out. The token is still checked. A wrong address would be worse than none.

The idempotency key lets a retry of the same siteverify call reuse Cloudflare's first answer instead of looking like a spent token. This step does not retry on its own. A second click in the browser is a new call with a new key, so a spent token still fails and the person is asked to complete the check again.

Failures:

- A missing token is 400 `turnstile_required`. That does not need the secret, so a preview without `TURNSTILE_SECRET` can still show it. A honeypot hit still returns 200 before this check.
- `timeout-or-duplicate`, or a `challenge_ts` older than 300 seconds, is 400 `turnstile_timeout`. The person can complete the widget again. A fake success would tell them the note was sent.
- Any other rejected token, hostname, or action is 400 `turnstile_failed`. The log names the hostname and the error code. It does not include the token or the secret.
- A missing secret, `invalid-input-secret`, `missing-input-secret`, `internal-error`, `bad-request`, an HTTP error, or a timeout talking to Cloudflare is 503 `turnstile_unavailable`. The process stays up. The check is not skipped. Accepting the post because Cloudflare was down would turn an outage into an open form. The person can try again when the call works. `invalid-input-secret` is logged as a configuration problem.

Cloudflare's published test secret that always passes returns `hostname: example.com`, a fresh `challenge_ts`, and no `action`, plus `metadata.result_with_testing_key: true`. That one hostname is allowed for a testing-key response, and a missing action is allowed only then. A production secret does not get that exception. `example.com` on a real secret is rejected.

**501.** After Turnstile passes, `NotImplementedContactSubmitter` returns 501 and drops the values. The seam is `IContactSubmitter.SubmitAsync(ValidatedContact)`. The next step that stores anything hashes the client IP and calls `dbo.usp_SubmitContact` with `@Name`, `@Email`, `@Message`, and `@IpHash`.

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

`ContactPipeline` is the order of checks. `FormTokenCodec` is the HMAC. `TimeProvider` is the clock, so a test can move time without sleeping. `IIpAddressHasher` is the seam for the IP HMAC. Nothing calls it yet. The database stores 32 bytes of HMAC, never the visitor's IP address.

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
5. The rate-limit slot runs. Today it allows the request.
6. Turnstile siteverify runs when `turnstileToken` is present and `TURNSTILE_SECRET` is set. Otherwise the response is the 400 or 503 from the contract table.
7. A request that passes asks `IContactSubmitter`. The registered class is `NotImplementedContactSubmitter`.
8. The response is 501 and `{ "error": "not_implemented" }`.

Every function uses `AuthorizationLevel.Anonymous`. A function key would block the browser. Static Web Apps is the front door, and on the Free plan the managed function is not reachable as its own public Function App.

## Settings

Copy the example before you run the host:

```bash
cp api/local.settings.json.example api/local.settings.json
```

`local.settings.json` is gitignored. The example is safe to commit because every secret is an empty string.

| Name                        | What it is                                                                                                                                   |
| --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `FUNCTIONS_WORKER_RUNTIME`  | `dotnet-isolated`. Tells the host which worker to start.                                                                                     |
| `AzureWebJobsStorage`       | Empty locally. HTTP-only functions do not need a storage account for these routes.                                                           |
| `SQL_CONNECTION_STRING`     | ADO.NET string for database `ssportfolio` on server `sethstearns-sql`, user `contact_api`. Not read yet.                                     |
| `TURNSTILE_SECRET`          | Cloudflare Turnstile secret for siteverify. Empty until you set it. The public site key is `turnstileSiteKey` in `src/config/site.ts`.       |
| `TURNSTILE_ALLOWED_HOSTS`   | Optional comma-separated hostnames added to the built-in allow list. Empty uses only the built-in names.                                     |
| `TURNSTILE_EXPECTED_ACTION` | Optional. Empty means `contact`.                                                                                                             |
| `IP_HASH_KEY`               | Secret key for the HMAC of the visitor IP. Anyone with this key can test addresses against the hashes.                                       |
| `FORM_TOKEN_KEY`            | Secret key for form tokens only. At least 16 characters. Generate about 40. The value is used exactly as stored, including a trailing space. |

In Azure, set the secrets under the Static Web App's **Environment variables** (also shown as Configuration / Application settings). Do not put them in `staticwebapp.config.json`. That file is in the repo and is public. The Functions host reads the same names from the environment, which is what `ContactApiOptionsBinder` looks up.

`FORM_TOKEN_KEY` and `TURNSTILE_SECRET` are the settings this API reads today. Add both in the portal when the later deploy step says to. Until `FORM_TOKEN_KEY` is there, `GET /api/contact/token` returns 503, and a POST that includes `formToken` returns 503. Until `TURNSTILE_SECRET` is there, a POST that includes `turnstileToken` and passes the cheap checks returns 503 `turnstile_unavailable`. Health, a honeypot POST, and a POST with no form token keep working. Do not commit a real key. An empty string in the example is the committed value.

Do not put Cloudflare's always-pass test secret in Azure. It accepts every dummy token. Paste it only into the gitignored `api/local.settings.json` when you want to call siteverify from your machine. The committed example stays empty so a copy of that file into the portal does not open the form.

Generating a password or a hash key:

```bash
python3 -c "import secrets,string; chars=string.ascii_letters+string.digits+'!@#$%^&*-_'; print(''.join(secrets.choice(chars) for _ in range(40)))"
```

The SQL user itself is created by `db/003_contact_api_user.sql`. This project does not connect yet.

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

Coverage includes the content-type and 8 KiB boundaries, the honeypot's fake 200, tokens that are too fast, expired, tampered, forged, or signed with a short key, link counting, HTML detection, Turnstile's pass, fail, and already-spent shapes against a mocked siteverify call, and the 501 that follows a valid body. The unit tests do not call Cloudflare.
