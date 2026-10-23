import { act, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import userEvent from '@testing-library/user-event';
import { ThemeToggle } from '../components/ThemeToggle.tsx';
import { THEME_STORAGE_KEY } from '../theme/storage.ts';
import { renderWithTheme } from './render.tsx';
import { setMediaQuery } from './setup.ts';

const DARK = '(prefers-color-scheme: dark)';

describe('theme toggle', () => {
  it('follows a dark operating system until someone chooses', () => {
    setMediaQuery(DARK, true);
    renderWithTheme(<ThemeToggle />);
    expect(document.documentElement).toHaveAttribute('data-theme', 'dark');
    expect(document.documentElement).toHaveAttribute('data-theme-preference', 'system');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBeNull();
  });

  it('follows a light operating system until someone chooses', () => {
    setMediaQuery(DARK, false);
    renderWithTheme(<ThemeToggle />);
    expect(document.documentElement).toHaveAttribute('data-theme', 'light');
  });

  it('saves a choice and announces the new theme', async () => {
    setMediaQuery(DARK, false);
    const user = userEvent.setup();
    renderWithTheme(<ThemeToggle />);
    await user.click(screen.getByRole('button', { name: 'Switch to dark theme' }));
    expect(document.documentElement).toHaveAttribute('data-theme', 'dark');
    expect(document.documentElement).toHaveAttribute('data-theme-preference', 'dark');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
    expect(
      screen.getByRole('button', { name: 'Switch to light theme' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Dark theme')).toBeInTheDocument();
  });

  it('lets a saved choice win over the operating system', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'light');
    setMediaQuery(DARK, true);
    renderWithTheme(<ThemeToggle />);
    expect(document.documentElement).toHaveAttribute('data-theme', 'light');
    expect(document.documentElement).toHaveAttribute('data-theme-preference', 'light');
  });

  it('keeps tracking the OS while the preference is still system', () => {
    setMediaQuery(DARK, false);
    renderWithTheme(<ThemeToggle />);
    act(() => setMediaQuery(DARK, true));
    expect(document.documentElement).toHaveAttribute('data-theme', 'dark');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBeNull();
  });

  it('ignores a later OS change after a choice is saved', async () => {
    setMediaQuery(DARK, false);
    const user = userEvent.setup();
    renderWithTheme(<ThemeToggle />);
    await user.click(screen.getByRole('button', { name: 'Switch to dark theme' }));
    act(() => setMediaQuery(DARK, false));
    expect(document.documentElement).toHaveAttribute('data-theme', 'dark');
  });
});
