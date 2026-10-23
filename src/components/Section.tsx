import type { ReactNode } from 'react';
import { cn } from '../lib/cn.ts';
import { Reveal } from './Reveal.tsx';

export function Section({
  id,
  index,
  title,
  children,
  spacing = 'default',
}: {
  id: string;
  index: string;
  title: string;
  children: ReactNode;
  /** "first" drops the top padding so the page does not start with a gap. */
  spacing?: 'default' | 'first';
}) {
  const headingId = `${id}-heading`;
  return (
    <section
      id={id}
      aria-labelledby={headingId}
      className={cn(
        // Clears the sticky mobile header when a nav link jumps here.
        'scroll-mt-32',
        spacing === 'default' ? 'py-12 sm:py-16' : 'pt-0 pb-12 sm:pb-16',
      )}
    >
      <h2
        id={headingId}
        className="text-ink flex items-baseline gap-3 text-2xl font-semibold tracking-tight"
      >
        <span className="text-accent font-mono text-sm font-medium">{index}</span>
        {title}
      </h2>
      <Reveal className="mt-6">{children}</Reveal>
    </section>
  );
}
