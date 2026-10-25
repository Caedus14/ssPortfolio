/// <reference types="node" />
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { pageUrl, siteUrl } from '../config/site.ts';
import { metaDescription, siteTitle } from '../data/content.ts';
import { THEME_STORAGE_KEY } from '../theme/storage.ts';

const html = readFileSync(
  resolve(dirname(fileURLToPath(import.meta.url)), '../../index.html'),
  'utf8',
);

describe('index.html', () => {
  it('matches the title and description Seth edits in the content module', () => {
    expect(html).toContain(`<title>${siteTitle}</title>`);
    expect(html).toContain(`content="${metaDescription}"`);
    expect(html).toContain('lang="en"');
  });

  it('boots the theme with the same storage key as the React provider', () => {
    expect(html).toContain(`var key = '${THEME_STORAGE_KEY}'`);
  });

  it('uses the public origin from the site config for canonical and social images', () => {
    expect(html).toContain(`<link rel="canonical" href="${pageUrl('/')}" />`);
    expect(html).toContain(`<meta property="og:url" content="${pageUrl('/')}" />`);
    expect(html).toContain(`<meta property="og:image" content="${siteUrl}/og.png" />`);
    expect(html).toContain(`<meta name="twitter:image" content="${siteUrl}/og.png" />`);
  });
});
