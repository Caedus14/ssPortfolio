import { useState } from 'react';
import { useTheme } from '../theme/theme-context.ts';
import { IconMoon, IconSun } from './icons.tsx';

/**
 * type="button" matters later: a contact form (Phase 2) would treat a button
 * without a type as a submit button.
 */
export function ThemeToggle() {
  const { theme, toggleTheme } = useTheme();
  const [announcement, setAnnouncement] = useState('');
  const nextIsLight = theme === 'dark';

  return (
    <>
      <button
        type="button"
        className="border-line text-ink hover:border-accent inline-flex size-11 items-center justify-center rounded-full border motion-safe:transition-colors"
        aria-label={nextIsLight ? 'Switch to light theme' : 'Switch to dark theme'}
        onClick={() => {
          toggleTheme();
          setAnnouncement(nextIsLight ? 'Light theme' : 'Dark theme');
        }}
      >
        {theme === 'dark' ? <IconSun /> : <IconMoon />}
      </button>
      <span className="sr-only" aria-live="polite">
        {announcement}
      </span>
    </>
  );
}
