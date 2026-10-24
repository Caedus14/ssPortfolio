import type { ReactNode } from 'react';
import { cn } from '../lib/cn.ts';
import { IconExternal } from './icons.tsx';

const defaultClass =
  'inline-flex items-center gap-1 font-semibold text-accent underline decoration-accent/40 underline-offset-4 hover:decoration-accent';

export function ExternalLink({
  href,
  children,
  className,
}: {
  href: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <a
      href={href}
      target="_blank"
      rel="noreferrer noopener"
      className={cn(className ?? defaultClass)}
    >
      {children}
      <IconExternal />
      <span className="sr-only"> (opens in a new tab)</span>
    </a>
  );
}
