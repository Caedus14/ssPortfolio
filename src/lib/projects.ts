import type { Project } from '../data/types.ts';

/** Tags in the order they first appear, so the filter does not jump around. */
export function projectTags(projects: readonly Project[]): string[] {
  const seen = new Set<string>();
  const tags: string[] = [];
  for (const project of projects) {
    for (const tag of project.tech) {
      if (seen.has(tag)) continue;
      seen.add(tag);
      tags.push(tag);
    }
  }
  return tags;
}

/** `null` means no filter (show every project). */
export function filterProjects(
  projects: readonly Project[],
  tag: string | null,
): Project[] {
  if (tag === null) return [...projects];
  return projects.filter((project) => project.tech.includes(tag));
}
