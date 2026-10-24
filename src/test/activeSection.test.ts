import { describe, expect, it } from 'vitest';
import { pickActiveSection } from '../lib/activeSection.ts';

const sections = [
  { id: 'about', top: 0 },
  { id: 'experience', top: 800 },
  { id: 'skills', top: 1600 },
];

describe('pickActiveSection', () => {
  it('keeps the first section until the marker crosses the next one', () => {
    expect(pickActiveSection(sections, 0)).toBe('about');
    expect(pickActiveSection(sections, 799)).toBe('about');
    expect(pickActiveSection(sections, 800)).toBe('experience');
    expect(pickActiveSection(sections, 2000)).toBe('skills');
  });

  it('returns an empty string when nothing is on the page', () => {
    expect(pickActiveSection([], 0)).toBe('');
  });
});
