import { useEffect } from 'react';
import { pageUrl } from '../config/site.ts';

function upsertMeta(
  attribute: 'name' | 'property',
  key: string,
  content: string,
): void {
  const selector = `meta[${attribute}="${key}"]`;
  let element = document.head.querySelector(selector);
  if (!element) {
    element = document.createElement('meta');
    element.setAttribute(attribute, key);
    document.head.append(element);
  }
  element.setAttribute('content', content);
}

function upsertCanonical(href: string): void {
  let element = document.head.querySelector('link[rel="canonical"]');
  if (!element) {
    element = document.createElement('link');
    element.setAttribute('rel', 'canonical');
    document.head.append(element);
  }
  element.setAttribute('href', href);
}

/**
 * Keeps the tab title, description, and canonical URL in step with the open
 * page. Social crawlers still read the tags in index.html; this updates them
 * after a client-side navigation, including /projects/:slug.
 */
export function usePageMeta(title: string, description: string, path: string): void {
  useEffect(() => {
    const url = pageUrl(path);
    document.title = title;
    upsertMeta('name', 'description', description);
    upsertMeta('property', 'og:title', title);
    upsertMeta('property', 'og:description', description);
    upsertMeta('property', 'og:url', url);
    upsertMeta('name', 'twitter:title', title);
    upsertMeta('name', 'twitter:description', description);
    upsertCanonical(url);
  }, [title, description, path]);
}
