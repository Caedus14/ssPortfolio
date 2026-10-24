import { Link } from 'react-router';
import { PageMain } from '../components/PageMain.tsx';
import { SiteFooter } from '../components/SiteFooter.tsx';
import { usePageMeta } from '../hooks/usePageMeta.ts';

export function NotFoundPage() {
  usePageMeta(
    'Page not found · Seth Stearns',
    'That link does not match a page on Seth Stearns’s site.',
  );

  return (
    <>
      <PageMain>
        <h1 className="text-ink font-serif text-4xl font-semibold tracking-tight">
          Page not found
        </h1>
        <p className="text-muted mt-4 max-w-prose">
          That link does not match anything on this site.
        </p>
        <p className="mt-6">
          <Link
            to="/"
            className="text-accent font-semibold underline-offset-4 hover:underline"
          >
            Back to the home page
          </Link>
        </p>
      </PageMain>
      <SiteFooter />
    </>
  );
}
