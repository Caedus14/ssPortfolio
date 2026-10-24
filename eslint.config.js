import js from '@eslint/js';
import prettier from 'eslint-config-prettier';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {
    ignores: ['dist', 'coverage', 'node_modules'],
  },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  jsxA11y.flatConfigs.recommended,
  reactHooks.configs.flat.recommended,
  reactRefresh.configs.vite,
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 2023,
      globals: globals.browser,
    },
    rules: {
      // The recommended hooks config leaves this as a warning.
      // CI runs ESLint with --max-warnings 0, so make it an error and fix it.
      'react-hooks/exhaustive-deps': 'error',
    },
  },
  {
    files: ['vite.config.ts', 'src/test/**/*.ts'],
    languageOptions: {
      globals: globals.node,
    },
  },
  prettier,
);
