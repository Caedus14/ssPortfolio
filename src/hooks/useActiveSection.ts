import { useEffect, useState } from 'react';
import { sectionIds } from '../data/content.ts';
import { pickActiveSection } from '../lib/activeSection.ts';

/**
 * Highlights the nav item for the section crossing a line about 30% down
 * the viewport. Measuring has to wait until after paint: the header renders
 * before the sections exist, so this cannot be derived during render.
 */
function measure(): string {
  const marker = window.scrollY + Math.min(window.innerHeight * 0.3, 220);
  const positions = [];
  for (const id of sectionIds) {
    const element = document.getElementById(id);
    if (!element) continue;
    positions.push({
      id,
      top: element.getBoundingClientRect().top + window.scrollY,
    });
  }
  return pickActiveSection(positions, marker);
}

export function useActiveSection(enabled: boolean): string {
  const [active, setActive] = useState(sectionIds[0] ?? '');

  useEffect(() => {
    if (!enabled) return;

    let frame = 0;
    const update = () => {
      frame = 0;
      setActive(measure());
    };
    const requestUpdate = () => {
      if (frame !== 0) return;
      frame = window.requestAnimationFrame(update);
    };

    requestUpdate();
    window.addEventListener('scroll', requestUpdate, { passive: true });
    window.addEventListener('resize', requestUpdate);
    window.addEventListener('hashchange', requestUpdate);

    return () => {
      window.removeEventListener('scroll', requestUpdate);
      window.removeEventListener('resize', requestUpdate);
      window.removeEventListener('hashchange', requestUpdate);
      if (frame !== 0) window.cancelAnimationFrame(frame);
    };
  }, [enabled]);

  return active;
}
