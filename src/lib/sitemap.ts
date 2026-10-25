import { pageUrl } from '../config/site.ts';
import type { Project } from '../data/types.ts';

function escapeXml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&apos;');
}

/**
 * Sitemap for the home page and each project detail route.
 * The Vite plugin writes this to dist/sitemap.xml at build time, so adding
 * a project in src/data/content.ts updates the sitemap without a second edit.
 */
export function renderSitemap(projects: readonly Project[]): string {
  const paths = ['/', ...projects.map((project) => `/projects/${project.slug}`)];
  const urls = paths
    .map((path) => `  <url>\n    <loc>${escapeXml(pageUrl(path))}</loc>\n  </url>`)
    .join('\n');

  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${urls}
</urlset>
`;
}
