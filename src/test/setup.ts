import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, beforeEach } from 'vitest';

type MediaListener = (event: MediaQueryListEvent) => void;

const queries = new Map<string, { matches: boolean; listeners: Set<MediaListener> }>();

function ensureQuery(query: string) {
  let entry = queries.get(query);
  if (!entry) {
    entry = { matches: false, listeners: new Set() };
    queries.set(query, entry);
  }
  return entry;
}

/** Test helper. Flip a media query and notify subscribers the way a browser would. */
export function setMediaQuery(query: string, matches: boolean) {
  const entry = ensureQuery(query);
  entry.matches = matches;
  const event = { matches, media: query } as MediaQueryListEvent;
  for (const listener of entry.listeners) {
    listener(event);
  }
}

beforeEach(() => {
  queries.clear();
  localStorage.clear();
  document.documentElement.removeAttribute('data-theme');
  document.documentElement.removeAttribute('data-theme-preference');
  document.documentElement.style.colorScheme = '';
});

afterEach(() => {
  cleanup();
});

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => {
    const entry = ensureQuery(query);
    return {
      get matches() {
        return entry.matches;
      },
      media: query,
      onchange: null,
      addEventListener: (_type: string, listener: MediaListener) => {
        entry.listeners.add(listener);
      },
      removeEventListener: (_type: string, listener: MediaListener) => {
        entry.listeners.delete(listener);
      },
      addListener: (listener: MediaListener) => {
        entry.listeners.add(listener);
      },
      removeListener: (listener: MediaListener) => {
        entry.listeners.delete(listener);
      },
      dispatchEvent: () => false,
    };
  },
});

class IntersectionObserverMock implements IntersectionObserver {
  readonly root: Element | Document | null = null;
  readonly rootMargin = '';
  readonly scrollMargin = '';
  readonly thresholds: readonly number[] = [];
  observe() {}
  unobserve() {}
  disconnect() {}
  takeRecords(): IntersectionObserverEntry[] {
    return [];
  }
}

Object.defineProperty(window, 'IntersectionObserver', {
  writable: true,
  value: IntersectionObserverMock,
});

Object.defineProperty(window, 'scrollTo', {
  writable: true,
  value: () => {},
});
