import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { loadEnv, type Plugin } from 'vite';
import { defineConfig } from 'vitest/config';

/**
 * Social crawlers do not run the React app, so Open Graph tags have to be in
 * index.html. The image stays root-relative ("/og.png") until VITE_SITE_URL is
 * a real https origin, because a crawler needs an absolute image URL.
 */
function siteUrlPlugin(siteUrl: string): Plugin {
  const safe = /^https?:\/\/[a-zA-Z0-9.-]+(?::\d+)?$/.test(siteUrl) ? siteUrl : '';
  const image = `${safe}/og.png`;
  const canonical = safe ? `<link rel="canonical" href="${safe}" />` : '';
  const ogUrl = safe ? `<meta property="og:url" content="${safe}" />` : '';

  return {
    name: 'site-url',
    transformIndexHtml(html) {
      return html
        .replaceAll('%OG_IMAGE%', image)
        .replaceAll('%CANONICAL%', canonical)
        .replaceAll('%OG_URL%', ogUrl);
    },
  };
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const siteUrl = (env.VITE_SITE_URL ?? '').trim().replace(/\/$/, '');

  return {
    plugins: [react(), tailwindcss(), siteUrlPlugin(siteUrl)],
    test: {
      environment: 'jsdom',
      setupFiles: './src/test/setup.ts',
      css: false,
    },
  };
});
