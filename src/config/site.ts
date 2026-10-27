/**
 * Phase seams. Phase 1 is a static site and does not call an API.
 *
 * Phase 2 — Azure Functions contact API (api/) + Azure SQL:
 *   POST /api/contact checks the body and Turnstile, then still returns 501.
 *   Set contactApi to true only when a later change adds the real form.
 *   ContactSection is the only UI that should grow a form.
 *   turnstileSiteKey is the public widget key. The secret stays on the API.
 *
 * Phase 3 — admin dashboard:
 *   Add a route in App.tsx. Forms there should edit the types in
 *   src/data/types.ts rather than inventing a second content shape.
 */

/**
 * Public origin, no trailing slash.
 *
 * https://sethstearns.dev is the custom domain. www.sethstearns.dev redirects
 * to this apex. index.html repeats the origin in the canonical and social
 * tags, because crawlers read that file before React runs. The meta test
 * fails if those copies drift apart.
 */
export const siteUrl = 'https://sethstearns.dev';

/**
 * Absolute URL for a route on this site.
 * The home page keeps a trailing slash: https://sethstearns.dev/
 */
export function pageUrl(path: string): string {
  if (path === '' || path === '/') return `${siteUrl}/`;
  const withSlash = path.startsWith('/') ? path : `/${path}`;
  const trimmed = withSlash.length > 1 ? withSlash.replace(/\/+$/, '') : withSlash;
  return `${siteUrl}${trimmed}`;
}

export const features = {
  contactApi: false,
} as const;

/**
 * Read this way so the Vite config can import `siteUrl` during `tsc -b`.
 * The Node program that typechecks vite.config.ts does not include Vite's
 * `import.meta.env` types. The browser build still injects the variable.
 */
const viteEnv = import.meta as ImportMeta & {
  env?: { VITE_CONTACT_API_URL?: string; VITE_TURNSTILE_SITE_KEY?: string };
};

export const contactApiUrl = viteEnv.env?.VITE_CONTACT_API_URL ?? '';

/**
 * Public Cloudflare Turnstile site key for the contact widget.
 * The secret is TURNSTILE_SECRET on the API and is not in this repository.
 * An empty VITE_TURNSTILE_SITE_KEY keeps this production key.
 * The later form renders the widget with action turnstileAction.
 */
export const turnstileSiteKey =
  viteEnv.env?.VITE_TURNSTILE_SITE_KEY || '0x4AAAAAAForbSDVPuig8KiS';

export const turnstileAction = 'contact';
