import { sections } from '../data/content.ts';
import { cn } from '../lib/cn.ts';

export function SectionNav({
  current,
  onNavigate,
  variant = 'sidebar',
}: {
  /** Active section id, or null when this page has no sections. */
  current: string | null;
  onNavigate?: () => void;
  variant?: 'sidebar' | 'menu';
}) {
  return (
    <nav aria-label="Sections">
      <ul className="m-0 list-none space-y-1 p-0">
        {sections.map((section) => {
          const active = current === section.id;
          return (
            <li key={section.id}>
              <a
                href={`/#${section.id}`}
                aria-current={active ? 'true' : undefined}
                onClick={onNavigate}
                className={cn(
                  'rounded-lg motion-safe:transition-colors',
                  variant === 'sidebar'
                    ? 'group flex items-center gap-4 py-2 text-xs font-semibold tracking-[0.16em] uppercase'
                    : 'block px-2 py-3 text-lg font-medium',
                  active ? 'text-ink' : 'text-muted hover:text-ink',
                )}
              >
                {variant === 'sidebar' ? (
                  <span
                    aria-hidden="true"
                    className={cn(
                      'h-px motion-safe:transition-[width,background-color] motion-safe:duration-200',
                      active ? 'bg-accent w-12' : 'w-6 bg-current group-hover:w-10',
                    )}
                  />
                ) : null}
                {section.label}
              </a>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
