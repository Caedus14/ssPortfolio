import { Link } from 'react-router';
import type { Project } from '../data/types.ts';
import { ExternalLink } from './ExternalLink.tsx';
import { ProjectCover } from './ProjectCover.tsx';
import { StatusBadge } from './StatusBadge.tsx';
import { TechList } from './TechList.tsx';

export function ProjectCard({ project }: { project: Project }) {
  return (
    <article className="border-line bg-surface shadow-card hover:border-accent rounded-2xl border p-5 motion-safe:transition-colors sm:p-6">
      <ProjectCover project={project} />
      <div className="mt-5">
        <StatusBadge status={project.status} />
      </div>
      <h3 className="mt-3 text-xl font-semibold tracking-tight">
        <Link
          to={`/projects/${project.slug}`}
          className="text-ink hover:text-accent underline-offset-4 hover:underline"
        >
          {project.title}
          <span aria-hidden="true"> →</span>
        </Link>
      </h3>
      <p className="text-muted mt-2 leading-relaxed text-pretty">{project.summary}</p>
      <div className="mt-4">
        <TechList items={project.tech} />
      </div>
      {project.demoUrl || project.repoUrl ? (
        <div className="mt-5 flex flex-wrap gap-x-5 gap-y-2 text-sm">
          {project.demoUrl ? (
            <ExternalLink href={project.demoUrl}>Live demo</ExternalLink>
          ) : null}
          {project.repoUrl ? (
            <ExternalLink href={project.repoUrl}>Source code</ExternalLink>
          ) : null}
        </div>
      ) : null}
    </article>
  );
}
