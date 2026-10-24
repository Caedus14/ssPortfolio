import { personJsonLd } from '../lib/jsonLd.ts';

/** JSON-LD cannot go through React children; stringify and escape `<`. */
export function JsonLd() {
  const json = JSON.stringify(personJsonLd()).replace(/</g, '\\u003c');
  return (
    <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: json }} />
  );
}
