# Contact API

This folder is the Azure Functions app for the contact form. It is a skeleton. `GET /api/health` returns a small JSON document. `POST /api/contact` returns HTTP 501 and does not save anything.

The React site does not show a form yet. `features.contactApi` in `src/config/site.ts` stays `false` until a later change wires the form to this API.

## Why it is shaped this way

The site is an Azure Static Web Apps Free app. On that plan the API is a **managed function**: Static Web Apps builds and hosts the Functions app for you. That comes with limits that decided the project shape:

- HTTP triggers only. A timer, a queue, or a Durable Functions workflow is not available here.
- No managed identity. The function signs in to SQL with the `contact_api` user and a connection string. It cannot use `DefaultAzureCredential`.
- A request is limited to 45 seconds. `host.json` sets `functionTimeout` to that same value so a local hang fails the way production will.
- Routes live under `/api`. The function route is `health` or `contact`. The host adds `/api`.

.NET 10 isolated is the runtime. Static Web Apps lists it as `dotnet-isolated:10.0` in `public/staticwebapp.config.json` (`platform.apiRuntime`). Support for the .NET 8 and .NET 9 isolated runtimes ends on 10 November 2026, which is why this project does not target those. .NET 10 on this host is isolated-worker only. The in-process model is not available for .NET 10.

"Isolated" means the function code runs in its own process, next to the Functions host. `Program.cs` is that process. `ConfigureFunctionsWebApplication()` lets a function use ASP.NET Core types (`HttpRequest`, `IActionResult`). It does not give you the full ASP.NET Core middleware pipeline, and it does not change the Static Web Apps limits above.

## Layout

```
api/
  Portfolio.Api.csproj          .NET 10 isolated worker
  Program.cs                    Host startup and dependency registration
  host.json                     45-second timeout, shared by every function
  local.settings.json.example   Empty settings. Copy this; do not commit the copy.
  Functions/                    HTTP edge. Health and the contact stub.
  Contact/                      Interfaces the later pipeline will fill in
  Options/                      Names of the three app settings
api.tests/                      xUnit tests. Not deployed.
```

`IContactSubmitter` is the seam for the later steps, in this order: validate the body, reject a honeypot or a too-fast submit, check `Origin`, apply an in-memory rate limit, verify Turnstile, hash the IP, then call `dbo.usp_SubmitContact`. The procedure's parameters and result codes are described in [`db/README.md`](../db/README.md) and copied as constants in `ContactProcedureCodes`. This skeleton does not call SQL.

`IIpAddressHasher` is the seam for the HMAC. Nothing calls it yet. The database stores 32 bytes of HMAC, never the visitor's IP address.

## How a request moves

`GET /api/health`

1. Static Web Apps receives the request and forwards `/api/*` to the function app.
2. The host matches the route `health` on `HealthFunction`.
3. The function returns 200 and `{ "status": "ok", "service": "ssportfolio-api" }`.

`POST /api/contact`

1. The same front door forwards the request.
2. `ContactFunction` does not read the body.
3. It asks `IContactSubmitter`. The registered class is `NotImplementedContactSubmitter`.
4. The response is 501 and `{ "error": "not_implemented" }`.

Both functions use `AuthorizationLevel.Anonymous`. A function key would block the browser. Static Web Apps is the front door, and on the Free plan the managed function is not reachable as its own public Function App.

## Settings

Copy the example before you run the host:

```bash
cp api/local.settings.json.example api/local.settings.json
```

`local.settings.json` is gitignored. The example is safe to commit because every secret is an empty string.

| Name                       | What it will be                                                                                         |
| -------------------------- | ------------------------------------------------------------------------------------------------------- |
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated`. Tells the host which worker to start.                                                |
| `AzureWebJobsStorage`      | Empty locally. HTTP-only functions do not need a storage account for this skeleton.                     |
| `SQL_CONNECTION_STRING`    | ADO.NET string for database `ssportfolio` on server `sethstearns-sql`, user `contact_api`.              |
| `TURNSTILE_SECRET`         | Cloudflare Turnstile secret. The site key, when the form exists, belongs in the frontend env, not here. |
| `IP_HASH_KEY`              | Secret key for the HMAC of the visitor IP. Anyone with this key can test addresses against the hashes.  |

In Azure, set the three secrets under the Static Web App's **Environment variables** (also shown as Configuration / Application settings). Do not put them in `staticwebapp.config.json`. That file is in the repo and is public. The Functions host reads the same names from the environment, which is what `ContactApiOptionsBinder` looks up.

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

`dotnet run --project api` does the same thing when Core Tools is installed. The SDK starts the Functions host for you.

The host listens on `http://localhost:7071`.

```bash
curl -sS http://127.0.0.1:7071/api/health
curl -sS -D- -o /dev/null -X POST http://127.0.0.1:7071/api/contact
```

The first command prints the health JSON. The second is HTTP 501.

With `AzureWebJobsStorage` left empty, the host log says the storage health check is unhealthy. That check is for queues and blobs, which this skeleton does not use. The HTTP routes still answer. Do not set `AzureWebJobsStorage` in the Static Web App. The platform fills that setting itself.

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

The tests call the function methods in process. They do not start the Functions host and they do not need the empty secrets. GitHub Actions runs the same two commands in the "Build and test the API" job, next to the existing frontend job.
