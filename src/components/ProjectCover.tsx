import type { Project } from '../data/types.ts';
import { useTheme } from '../theme/theme-context.ts';

/**
 * An `<img>` cannot see the page's CSS variables, so each theme has its own
 * file. Both `src` values can point at one photograph later. Below-the-fold
 * covers use loading="lazy"; do not lazy-load an image in the first screen.
 */
export function ProjectCover({ project }: { project: Project }) {
  const cover = project.cover;
  const { theme } = useTheme();
  if (!cover) return null;

  return (
    <img
      src={theme === 'dark' ? cover.darkSrc : cover.lightSrc}
      alt={cover.alt}
      width={cover.width}
      height={cover.height}
      loading="lazy"
      decoding="async"
      className="border-line bg-chip aspect-video w-full rounded-xl border object-cover"
    />
  );
}
