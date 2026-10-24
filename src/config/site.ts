/**
 * Phase seams. Phase 1 is a static site and does not call an API.
 *
 * Phase 2 — ASP.NET Core contact API + Azure SQL:
 *   Set contactApi to true and VITE_CONTACT_API_URL to the API origin.
 *   ContactSection is the only UI that should grow a form.
 *
 * Phase 3 — admin dashboard:
 *   Add a route in App.tsx. Forms there should edit the types in
 *   src/data/types.ts rather than inventing a second content shape.
 *
 * VITE_SITE_URL is the public origin, no trailing slash, for example
 * https://your-app.azurestaticapps.net. Leave it unset until that hostname exists.
 * A value that is not an http(s) origin is ignored.
 */
const rawSiteUrl = (import.meta.env.VITE_SITE_URL ?? '').trim().replace(/\/$/, '');

export const siteUrl = /^https?:\/\/[a-zA-Z0-9.-]+(?::\d+)?$/.test(rawSiteUrl)
  ? rawSiteUrl
  : '';

export const features = {
  contactApi: false,
} as const;

export const contactApiUrl = import.meta.env.VITE_CONTACT_API_URL ?? '';
