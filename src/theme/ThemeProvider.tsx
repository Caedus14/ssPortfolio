import {
  useCallback,
  useLayoutEffect,
  useMemo,
  useState,
  useSyncExternalStore,
} from 'react';
import type { ReactNode } from 'react';
import { ThemeContext } from './theme-context.ts';
import {
  readThemePreference,
  resolveTheme,
  systemPrefersDark,
  writeThemePreference,
  type ResolvedTheme,
  type ThemePreference,
} from './storage.ts';

/**
 * The OS theme is an external store: we subscribe instead of copying it into
 * state inside an effect. A saved "light" or "dark" choice overrides it.
 * Nothing is written to localStorage until the visitor uses the toggle, so a
 * later OS change still flows through while the preference is "system".
 */
function subscribeToSystemTheme(onChange: () => void): () => void {
  const media = window.matchMedia('(prefers-color-scheme: dark)');
  media.addEventListener('change', onChange);
  return () => media.removeEventListener('change', onChange);
}

function applyTheme(theme: ResolvedTheme, preference: ThemePreference): void {
  const root = document.documentElement;
  root.dataset.theme = theme;
  root.dataset.themePreference = preference;
  root.style.colorScheme = theme;

  // The browser chrome (mobile address bar) follows the canvas token so the
  // hex value does not have to be copied into TypeScript.
  const meta = document.querySelector('meta[name="theme-color"]');
  if (!(meta instanceof HTMLMetaElement)) return;
  const canvas = getComputedStyle(root).getPropertyValue('--canvas').trim();
  if (canvas) meta.setAttribute('content', canvas);
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreference] = useState<ThemePreference>(() =>
    readThemePreference(),
  );
  const systemDark = useSyncExternalStore(
    subscribeToSystemTheme,
    systemPrefersDark,
    () => false,
  );
  const theme = resolveTheme(preference, systemDark);

  useLayoutEffect(() => {
    applyTheme(theme, preference);
  }, [theme, preference]);

  const toggleTheme = useCallback(() => {
    const next: ResolvedTheme = theme === 'dark' ? 'light' : 'dark';
    writeThemePreference(next);
    applyTheme(next, next);
    setPreference(next);
  }, [theme]);

  const value = useMemo(
    () => ({ theme, preference, toggleTheme }),
    [theme, preference, toggleTheme],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}
