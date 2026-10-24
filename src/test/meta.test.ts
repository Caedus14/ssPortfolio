/// <reference types="node" />
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
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
});
