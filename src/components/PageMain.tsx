import type { ReactNode } from 'react';

export function PageMain({ children }: { children: ReactNode }) {
  return (
    <main
      id="main"
      // tabindex=-1 lets the skip link move focus here. The ring is left off
      // because it would outline the whole page; .skip-link carries the visible focus.
      tabIndex={-1}
      className="outline-none"
    >
      {children}
    </main>
  );
}
