import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import type { Plugin } from 'vite';
import { defineConfig } from 'vitest/config';
import { projects } from './src/data/content.ts';
import { renderSitemap } from './src/lib/sitemap.ts';

/**
 * Writes sitemap.xml from the project list. Dev serves it in memory.
 * `vite build` emits it next to index.html so Azure can serve the file.
 */
function sitemapPlugin(): Plugin {
  const xml = () => renderSitemap(projects);

  return {
    name: 'sitemap',
    configureServer(server) {
      server.middlewares.use((req, res, next) => {
        const path = req.url?.split('?')[0];
        if (path !== '/sitemap.xml') {
          next();
          return;
        }
        res.setHeader('Content-Type', 'application/xml; charset=utf-8');
        res.end(xml());
      });
    },
    generateBundle() {
      this.emitFile({
        type: 'asset',
        fileName: 'sitemap.xml',
        source: xml(),
      });
    },
  };
}

export default defineConfig({
  plugins: [react(), tailwindcss(), sitemapPlugin()],
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    css: false,
  },
});
