import { useState } from 'react';
import { projects, projectsNote, sectionById } from '../../data/content.ts';
import { filterProjects, projectTags } from '../../lib/projects.ts';
import { ProjectCard } from '../ProjectCard.tsx';
import { ProjectFilter } from '../ProjectFilter.tsx';
import { Section } from '../Section.tsx';

const section = sectionById('projects');

export function ProjectsSection() {
  const [tag, setTag] = useState<string | null>(null);
  const [message, setMessage] = useState('');
  const tags = projectTags(projects);
  const visible = filterProjects(projects, tag);

  function choose(next: string | null) {
    setTag(next);
    const count = filterProjects(projects, next).length;
    const noun = count === 1 ? 'project' : 'projects';
    setMessage(
      next === null
        ? `Showing all ${count} projects`
        : `Showing ${count} ${noun} tagged ${next}`,
    );
  }

  return (
    <Section id={section.id} index={section.index} title={section.heading}>
      <p className="text-muted leading-relaxed text-pretty">{projectsNote}</p>
      <div className="mt-6">
        <ProjectFilter tags={tags} active={tag} onChange={choose} />
      </div>
      <p className="sr-only" aria-live="polite">
        {message}
      </p>
      {visible.length === 0 ? (
        <p className="text-muted mt-6">No projects use that tag.</p>
      ) : (
        <ul className="mt-6 list-none space-y-6 p-0">
          {visible.map((project) => (
            <li key={project.slug}>
              <ProjectCard project={project} />
            </li>
          ))}
        </ul>
      )}
    </Section>
  );
}
