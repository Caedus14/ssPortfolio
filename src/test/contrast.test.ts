/// <reference types="node" />
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const css = readFileSync(
  resolve(dirname(fileURLToPath(import.meta.url)), '../index.css'),
  'utf8',
);

function varsIn(slice: string): Record<string, string> {
  const vars: Record<string, string> = {};
  for (const match of slice.matchAll(/--([a-z0-9-]+):\s*(#[0-9a-fA-F]{6})\s*;/g)) {
    const name = match[1];
    const value = match[2];
    if (!name || !value) continue;
    vars[name] = value.toLowerCase();
  }
  return vars;
}

function sliceBetween(source: string, start: string, end: string): string {
  const from = source.indexOf(start);
  if (from < 0) throw new Error(`Could not find "${start}" in src/index.css`);
  const to = source.indexOf(end, from + start.length);
  if (to < 0) throw new Error(`Could not find "${end}" after "${start}"`);
  return source.slice(from, to);
}

function channel(value: number): number {
  const scaled = value / 255;
  return scaled <= 0.03928 ? scaled / 12.92 : ((scaled + 0.055) / 1.055) ** 2.4;
}

function contrast(foreground: string, background: string): number {
  const lum = (hex: string) => {
    const n = Number.parseInt(hex.slice(1), 16);
    const r = channel((n >> 16) & 255);
    const g = channel((n >> 8) & 255);
    const b = channel(n & 255);
    return 0.2126 * r + 0.7152 * g + 0.0722 * b;
  };
  const lighter = Math.max(lum(foreground), lum(background));
  const darker = Math.min(lum(foreground), lum(background));
  return (lighter + 0.05) / (darker + 0.05);
}

const pairs = [
  ['ink', 'canvas'],
  ['ink', 'surface'],
  ['muted', 'canvas'],
  ['muted', 'surface'],
  ['accent', 'canvas'],
  ['accent', 'surface'],
  ['accent', 'chip'],
  ['accent-ink', 'accent'],
  ['live-fg', 'live-bg'],
  ['progress-fg', 'progress-bg'],
  ['archived-fg', 'archived-bg'],
] as const;

const light = varsIn(sliceBetween(css, ':root,', "[data-theme='dark']"));
const dark = varsIn(
  sliceBetween(css, "[data-theme='dark']", '@media (prefers-color-scheme: dark)'),
);
const noJsDark = varsIn(
  sliceBetween(css, ":root:not([data-theme='light'])", '@theme inline'),
);

describe('color tokens', () => {
  it('uses the same dark palette when JavaScript has not run', () => {
    expect(noJsDark).toEqual(dark);
  });

  it.each([
    ['light', light],
    ['dark', dark],
  ] as const)('%s text meets WCAG AA (4.5:1)', (_name, tokens) => {
    for (const [foreground, background] of pairs) {
      const fg = tokens[foreground];
      const bg = tokens[background];
      expect(fg, foreground).toBeDefined();
      expect(bg, background).toBeDefined();
      expect(
        contrast(fg ?? '#000000', bg ?? '#ffffff'),
        `${foreground} on ${background}`,
      ).toBeGreaterThanOrEqual(4.5);
    }
  });
});
