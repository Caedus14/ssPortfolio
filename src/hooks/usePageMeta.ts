import { useEffect } from 'react';

function setContent(selector: string, value: string): void {
  const element = document.head.querySelector(selector);
  if (element) element.setAttribute('content', value);
}

/** Keeps the tab title and the social tags in step with the open page. */
export function usePageMeta(title: string, description: string): void {
  useEffect(() => {
    document.title = title;
    setContent('meta[name="description"]', description);
    setContent('meta[property="og:title"]', title);
    setContent('meta[property="og:description"]', description);
    setContent('meta[name="twitter:title"]', title);
    setContent('meta[name="twitter:description"]', description);
  }, [title, description]);
}
