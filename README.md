# Seth Stearns — portfolio

Personal portfolio and resume site for Seth Stearns, an application developer working in C# / .NET. It is live at [https://sethstearns.dev](https://sethstearns.dev). The SQL scripts for the contact database are in [`db/`](db/README.md). The contact API skeleton is in [`api/`](api/README.md). The page still does not show a form.

Later phases can sit beside this without a rewrite:

- **Phase 2** adds the contact API and Azure SQL. The database scripts are in [`db/`](db/README.md). The API project is in [`api/`](api/README.md): .NET 10 isolated Functions, a health check, and a contact route that returns 501. The switch for the form is `features.contactApi` in `src/config/site.ts`. The contact section is the only place a form should appear.
- **Phase 3** adds an admin dashboard. It should edit the same shapes in `src/data/types.ts` instead of inventing a second content model.

The repository is public. There is no license file, by choice. See [Rights](#rights).

## Tech stack

| Piece                                                                           | Role                                                                                                                                                |
| ------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| [React 19](https://react.dev/)                                                  | UI components.                                                                                                                                      |
| [TypeScript](https://www.typescriptlang.org/) (strict)                          | Types for content and components, so a typo in the resume data fails the build.                                                                     |
| [Vite](https://vite.dev/)                                                       | Dev server and production bundler. `npm run dev` reloads as you edit. `npm run build` writes static files to `dist/`.                               |
| [Tailwind CSS 4](https://tailwindcss.com/)                                      | Utility classes for layout and spacing. Colors are CSS variables, so light and dark share one set of class names.                                   |
| [Framer Motion](https://www.framer.com/motion/)                                 | Short entrance motion on sections. It is skipped when the visitor asks for reduced motion.                                                          |
| [React Router](https://reactrouter.com/)                                        | Home page, `/projects/:slug` detail pages, and a not-found page.                                                                                    |
| [Vitest](https://vitest.dev/) + [Testing Library](https://testing-library.com/) | Tests that render the page the way a visitor would: click a filter, toggle the theme, read the text.                                                |
| ESLint + Prettier                                                               | Lint rules (including accessibility) and one formatting style.                                                                                      |
| GitHub Actions                                                                  | On every push to `main` and every pull request: lint, format check, typecheck, test, and build. A second job runs `dotnet build` and `dotnet test`. |
| .NET 10 isolated Functions                                                      | The contact API in `api/`. Static Web Apps hosts it as a managed function. See [`api/README.md`](api/README.md).                                    |

Fonts (Outfit and Fraunces) are bundled from `@fontsource`, Latin subset only. The site does not call Google Fonts or an analytics service.

## Folder structure

```
index.html                  Title, meta tags, and the theme script that runs before React
public/                     Files copied into dist/ as-is
  favicon.svg
  apple-touch-icon.png
  og.png                    Social preview image
  robots.txt                Allows all crawlers and points at the sitemap
  site.webmanifest
  staticwebapp.config.json  Azure Static Web Apps routing and headers
  images/                   Decorative project covers (SVG)
src/
  main.tsx                  React entry
  App.tsx                   Theme, router, and the two-column shell
  index.css                 Color tokens, focus ring, reduced-motion rules
  config/site.ts            Public site URL and phase feature flags
  data/
    types.ts                TypeScript shapes (Profile, Experience, Project, …)
    content.ts              Every word on the site. Edit this, not the components.
  theme/                    Light / dark preference and localStorage
  hooks/                    Scroll-spy, per-page title, and canonical URL
  lib/                      Small pure functions (filter, sitemap, JSON-LD)
  components/               Header, sections, cards, links
  pages/                    Home, project detail, not found
  test/                     Vitest tests
.github/workflows/ci.yml
api/                        .NET 10 isolated Functions for the contact form (see api/README.md)
api.tests/                  xUnit tests for that Functions project
db/                         Azure SQL scripts for the contact form (see db/README.md)
  001_tables.sql
  002_usp_SubmitContact.sql
  003_contact_api_user.sql
```

On a wide screen the header stays fixed on the left (name, title, section links, social links) and the sections scroll on the right. On a narrow screen the layout is one column and the section links move into a menu button.

## Run it locally

You need [Node.js 22](https://nodejs.org/) or newer.

```bash
npm ci          # install the exact versions in package-lock.json
npm run dev     # http://localhost:5173
```

`npm ci` is the same command CI uses. The repo includes an `.npmrc` with `legacy-peer-deps=true` because npm 10's resolver crashes on this dependency tree (`Cannot read properties of null (reading 'edgesOut')`). The lockfile still pins versions.

The API runs beside the site. You need the .NET 10 SDK and Azure Functions Core Tools v4. Copy `api/local.settings.json.example` to `api/local.settings.json` (that copy is gitignored), then:

```bash
dotnet test ssportfolio.sln
cd api && func start          # http://localhost:7071/api/health
npm run dev                   # http://localhost:5173/api/health proxies to the host
```

`npm run dev` forwards `/api` to port 7071. The production build does not. Azure routes `/api` itself. Details, including the Static Web Apps CLI, are in [`api/README.md`](api/README.md).

Other scripts:

| Script                 | What it does                                   |
| ---------------------- | ---------------------------------------------- |
| `npm run dev`          | Vite dev server with hot reload.               |
| `npm test`             | Run the test suite once.                       |
| `npm run test:watch`   | Re-run tests as files change.                  |
| `npm run lint`         | ESLint, zero warnings allowed.                 |
| `npm run format`       | Prettier write.                                |
| `npm run format:check` | Prettier check (CI uses this).                 |
| `npm run typecheck`    | `tsc -b` with the strict app and Node configs. |
| `npm run build`        | Typecheck, then write `dist/`.                 |
| `npm run preview`      | Serve the production build locally.            |

## How to edit content

Open `src/data/content.ts`. The shapes are in `src/data/types.ts`. If a field is missing or the wrong type, `npm run typecheck` (and the dev server) will say so.

A few rules that keep the page honest:

- Facts come from the resume. Leave out employers, dates, metrics, and projects that are not already written down.
- Public location is city and state (`Eau Claire, WI`). Leave the phone number, street, and ZIP off the site, including JSON-LD in `src/lib/jsonLd.ts`.
- Experience `tech` tags should be tools that role's bullets already name.
- `demoUrl` and `repoUrl` stay `null` until a real URL exists. The detail page then says that nothing public is listed.
- Comments marked `PLACEHOLDER` are real work (the homelab, and this website) that is not a client case study. Keep that wording if you extend them.
- The homelab status `live` means the lab is still running. It is a private network, so it has no demo link.
- Project covers in `public/images/` are decorative drawings (`alt: ""`). When you add a real screenshot, set `alt` to a short description of what the picture shows, and add `loading="lazy"` is already applied by `ProjectCover`.

### Add a skill

Add a string to the right group in `skillGroups`. To add a group, append an object with a unique `id`, a `title`, and `items`.

### Add a project

Append an object to `projects`. `slug` becomes the URL (`/projects/your-slug`) and must be unique. `status` is `'live'`, `'in-progress'`, or `'archived'`. Tech tags on the home page are collected from every project's `tech` array, so a new tag shows up in the filter on its own.

```ts
{
  slug: 'example',
  title: 'Example',
  summary: 'One or two sentences already supported by highlights.',
  context: null,
  highlights: ['A sentence you can stand behind.'],
  status: 'in-progress',
  tech: ['C#', 'ASP.NET'],
  demoUrl: null,
  repoUrl: null,
  cover: null,
}
```

The page title and the meta description are also in `content.ts` (`siteTitle`, `metaDescription`). The same strings are copied into `index.html` for the first paint and for crawlers that do not run JavaScript. If you change them, update both places. `src/test/meta.test.ts` fails when they drift apart.

### Theme

The first visit follows the operating system (`prefers-color-scheme`). Choosing light or dark writes `theme` to `localStorage` and sticks. Clearing that key returns to the system choice.

`index.html` sets `data-theme` in a small inline script before the bundle loads, so the page does not flash the wrong colors. The storage key in that script must match `THEME_STORAGE_KEY` in `src/theme/storage.ts`. A test checks that.

Colors live as CSS variables in `src/index.css`. Tailwind utilities such as `bg-canvas` and `text-ink` point at those variables, so components do not carry separate `dark:` color classes. `src/test/contrast.test.ts` checks the text pairs against WCAG AA (4.5:1) and checks that the no-JavaScript dark block matches the dark theme block.

### Resume PDF

The Download resume button links to `/resume.pdf` and asks the browser to save the file as `Seth_Stearns_Resume.pdf`. On Azure, `public/staticwebapp.config.json` sends the same name in a `Content-Disposition` header, so the saved file keeps that name when the browser ignores the `download` attribute.

The file that ships is `public/resume.pdf`. It is committed. It lists Eau Claire, WI and leaves out a phone number, a street address, and a ZIP code.

If you replace that file, read the new export before you commit it. The committed PDF has to stay phone-free.

## Deploy to Azure Static Web Apps

### Public URL

The public origin is `https://sethstearns.dev`, defined once in `src/config/site.ts`. `www.sethstearns.dev` redirects to that apex. `index.html` repeats the same origin in the canonical link, `og:url`, `og:image`, and `twitter:image`, because crawlers read that file before JavaScript runs. After a client-side navigation, `usePageMeta` points `og:url` and the canonical link at the open route, including `/projects/:slug`. A test fails if `index.html` and `siteUrl` drift apart.

### Custom domain

DNS for the custom domain is on Cloudflare. `@` and `www` are DNS-only CNAMEs (grey cloud, so Cloudflare does not proxy the records) to `nice-rock-033c92210.2.azurestaticapps.net`. Azure's TXT validation record has to stay. Removing that TXT record makes Azure drop the custom domain.

GitHub Actions CI only checks the code. `.github/workflows/azure-static-web-apps-nice-rock-033c92210.yml` builds the site and deploys `dist` to Azure Static Web Apps. The deployment token stays in GitHub secrets.

| Setting         | Value                  |
| --------------- | ---------------------- |
| App location    | `dist`                 |
| Output location | empty                  |
| API location    | `api`                  |
| API runtime     | `dotnet-isolated:10.0` |

The workflow builds the site with `npm ci` and `npm run build`, then deploys that `dist` folder with `skip_app_build`. The app location is `dist` itself, not the repo root. `npm ci` creates `node_modules`, and uploading the root would send those files too. Azure then rejects the deploy because the static file count is too high. Oryx still publishes the Functions app from `api/`. `public/staticwebapp.config.json` sets `platform.apiRuntime` to `dotnet-isolated:10.0`, which is the Static Web Apps name for the .NET 10 isolated worker.

Secrets for the API (`SQL_CONNECTION_STRING`, `TURNSTILE_SECRET`, `IP_HASH_KEY`) go in the Static Web App's Environment variables. They do not go in the repo. The contact route does not read them yet.

`public/staticwebapp.config.json` is copied into `dist/` by Vite. It does three jobs:

- Sends unknown paths (such as `/projects/homelab`) to `index.html` so React Router can render them. Files that exist on disk, including `*.pdf`, `/resume.pdf`, `/robots.txt`, and `/sitemap.xml`, are excluded and served directly.
- Sets basic response headers: `nosniff`, a strict referrer policy, `X-Frame-Options: DENY`, and a permissions policy that turns the camera, microphone, and geolocation off.
- Declares MIME types for `.webmanifest`, `.pdf`, and `.xml`.

`vite build` writes `dist/sitemap.xml` from the project list in `src/data/content.ts`. The file is generated instead of hand-edited, so a new project shows up in the sitemap on the next build. `public/robots.txt` allows every crawler and lists `https://sethstearns.dev/sitemap.xml`.

`VITE_CONTACT_API_URL` is reserved for Phase 2. It is unused while `features.contactApi` is `false`. Copy `.env.example` to `.env` when you need that variable. `.env` is gitignored.

One tradeoff of the SPA fallback: a path that does not match a project still returns HTTP 200 with the "Page not found" screen. Azure is serving `index.html` on purpose so client-side routes work. A true 404 status would need a Static Web Apps function or a route rule per project slug.

A Content-Security-Policy header is not set. The theme script in `index.html` is inline, and a strict policy would block it unless the policy includes a hash of that script. Add the hash in the same change that adds the policy.

## Accessibility

- Landmarks: skip link, `header`, `nav` ("Sections"), `main`, `footer`.
- One `h1` per page (the name on the home page, the project title on a detail page).
- Visible `:focus-visible` outline. Icon buttons are at least 44px.
- External links open in a new tab with `rel="noreferrer noopener"` and a screen-reader note.
- `prefers-reduced-motion` disables the Framer Motion offset and shortens CSS transitions. Section text stays fully opaque so it is not invisible while it animates.
- Cover images use `alt=""` while they are decorative, and `loading="lazy"`.

## Tests

`src/test/` covers the behavior that is easy to break while editing content or theme code:

- Project filter shows and hides cards, and the pressed state follows the selected tag.
- Theme toggle writes `localStorage` and follows the system preference until a choice is saved.
- Home page includes resume facts, landmarks, and the `/resume.pdf` link.
- Rendered text and the content module contain no phone number, street, or ZIP.
- Contrast tokens meet WCAG AA, and `index.html` matches `content.ts` and `siteUrl`.
- The sitemap lists the home page and every project, and Azure is told to serve `sitemap.xml` as XML.

Tests render with `css: false`, so Tailwind's `hidden` / `lg:block` do not remove elements in jsdom. Both the mobile and desktop controls are in the document. Queries that can match twice (the theme button, LinkedIn) use `getAllByRole`.

## Rights

The source code is public so it can be read and deployed. No license is granted. All rights reserved.

The resume wording, employment history, and other personal content belong to Seth Stearns. Copying the code does not include permission to reuse that content.
