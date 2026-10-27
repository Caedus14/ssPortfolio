/// <reference types="node" />
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { pageUrl, siteUrl } from '../config/site.ts';
import { projects } from '../data/content.ts';
import { renderSitemap } from '../lib/sitemap.ts';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

describe('sitemap', () => {
  it('lists the home page and every project from the content module', () => {
    const xml = renderSitemap(projects);
    expect(xml).toContain(`<loc>${pageUrl('/')}</loc>`);
    for (const project of projects) {
      expect(xml).toContain(`<loc>${pageUrl(`/projects/${project.slug}`)}</loc>`);
    }
    expect(xml.match(/<loc>/g)?.length).toBe(1 + projects.length);
  });
});

describe('crawler files', () => {
  it('points robots.txt at the sitemap on the public origin', () => {
    const robots = readFileSync(resolve(root, 'public/robots.txt'), 'utf8');
    expect(robots).toContain('Allow: /');
    expect(robots).toContain(`Sitemap: ${siteUrl}/sitemap.xml`);
  });

  it('serves sitemap.xml as a file instead of the SPA fallback', () => {
    const config = readFileSync(
      resolve(root, 'public/staticwebapp.config.json'),
      'utf8',
    );
    expect(config).toContain('"/sitemap.xml"');
    expect(config).toContain('"*.xml"');
    expect(config).toContain('"application/xml"');
    expect(config).toContain('"dotnet-isolated:10.0"');
  });
});
