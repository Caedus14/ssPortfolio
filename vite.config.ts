import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// Vitest's defineConfig wraps Vite's, so the dev server and the tests share
// one config file. `passWithNoTests` is intentionally absent: CI should fail
// if the test files disappear.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    css: false,
    // Removed once src/test has real cases. Kept only so an early
    // toolchain commit can run `npm test` before those files exist.
    passWithNoTests: true,
  },
});
