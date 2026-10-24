import { useCallback, useState } from 'react';
import { Link, useLocation } from 'react-router';
import { profile } from '../data/content.ts';
import { useActiveSection } from '../hooks/useActiveSection.ts';
import { formatLocation } from '../lib/format.ts';
import { IconClose, IconMenu } from './icons.tsx';
import { MobileMenu } from './MobileMenu.tsx';
import { ResumeLink } from './ResumeLink.tsx';
import { SectionNav } from './SectionNav.tsx';
import { SocialLinks } from './SocialLinks.tsx';
import { ThemeToggle } from './ThemeToggle.tsx';

function NameBlock({ home }: { home: boolean }) {
  const className =
    'font-serif text-[2.35rem] leading-[1.05] font-semibold tracking-tight text-balance text-ink sm:text-5xl';

  return (
    <div className="min-w-0">
      {home ? (
        <h1 className={className}>{profile.name}</h1>
      ) : (
        <p className={className}>
          <Link to="/" className="text-ink">
            {profile.name}
          </Link>
        </p>
      )}
      <p className="text-accent mt-3 text-base font-medium lg:text-lg">
        {profile.title}
      </p>
      <p className="text-muted mt-1 hidden text-sm lg:block">{profile.headline}</p>
    </div>
  );
}

export function SiteHeader() {
  const { pathname } = useLocation();
  const onHome = pathname === '/';
  const active = useActiveSection(onHome);
  const current = onHome ? active : null;
  const [menuOpen, setMenuOpen] = useState(false);
  const closeMenu = useCallback(() => {
    setMenuOpen(false);
  }, []);

  return (
    <header className="border-line bg-canvas sticky top-0 z-40 border-b px-5 py-4 lg:flex lg:h-svh lg:max-h-svh lg:flex-col lg:overflow-y-auto lg:border-r lg:border-b-0 lg:px-10 lg:py-12">
      <div className="flex items-start justify-between gap-3">
        <NameBlock home={onHome} />
        <div className="flex shrink-0 items-center gap-2 lg:hidden">
          <ThemeToggle />
          <button
            type="button"
            className="border-line text-ink inline-flex size-11 items-center justify-center rounded-full border"
            aria-expanded={menuOpen}
            aria-controls={menuOpen ? 'mobile-menu' : undefined}
            aria-label={menuOpen ? 'Close menu' : 'Open menu'}
            onClick={() => (menuOpen ? closeMenu() : setMenuOpen(true))}
          >
            {menuOpen ? <IconClose /> : <IconMenu />}
          </button>
        </div>
      </div>

      <p className="text-muted mt-5 hidden text-sm leading-relaxed text-pretty lg:block">
        {profile.summary[0]}
      </p>
      <p className="text-muted mt-4 hidden text-sm lg:block">
        {formatLocation(profile.location)}
      </p>
      <span className="bg-accent mt-6 hidden h-px w-12 lg:block" aria-hidden="true" />

      <div className="mt-8 hidden lg:block">
        <SectionNav current={current} />
      </div>

      <div className="mt-auto hidden flex-col gap-6 pt-10 lg:flex">
        <ResumeLink className="w-full" />
        <div className="flex items-center justify-between gap-3">
          <SocialLinks />
          <ThemeToggle />
        </div>
      </div>

      {menuOpen ? <MobileMenu current={current} onClose={closeMenu} /> : null}
    </header>
  );
}
