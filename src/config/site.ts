/**
 * Phase seams. Phase 1 is a static site and does not call an API.
 *
 * Phase 2 — Azure Functions contact API (api/) + Azure SQL:
 *   The skeleton is deployed, and POST /api/contact is still a 501.
 *   Set contactApi to true only when a later change adds the real form.
 *   ContactSection is the only UI that should grow a form.
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
  env?: { VITE_CONTACT_API_URL?: string };
};

export const contactApiUrl = viteEnv.env?.VITE_CONTACT_API_URL ?? '';
