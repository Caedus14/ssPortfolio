import { pageUrl } from '../config/site.ts';
import { links, profile } from '../data/content.ts';

/**
 * schema.org Person for search engines. There is no `telephone` and no
 * `streetAddress` on purpose.
 */
export function personJsonLd(): Record<string, unknown> {
  const data: Record<string, unknown> = {
    '@context': 'https://schema.org',
    '@type': 'Person',
    name: profile.name,
    jobTitle: profile.title,
    email: `mailto:${profile.email}`,
    address: {
      '@type': 'PostalAddress',
      addressLocality: profile.location.city,
      addressRegion: profile.location.region,
      addressCountry: profile.location.country,
    },
    sameAs: [links.github, links.linkedin],
    knowsAbout: ['C#', '.NET', 'Microsoft Azure', 'REST APIs', 'SQL Server'],
  };

  data.url = pageUrl('/');
  return data;
}
