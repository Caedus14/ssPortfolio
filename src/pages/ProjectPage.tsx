import { useParams } from 'react-router';
import { PageMain } from '../components/PageMain.tsx';
import { ProjectCover } from '../components/ProjectCover.tsx';
import { SiteFooter } from '../components/SiteFooter.tsx';
import { StatusBadge } from '../components/StatusBadge.tsx';
import { TechList } from '../components/TechList.tsx';
import { ExternalLink } from '../components/ExternalLink.tsx';
import { projects } from '../data/content.ts';
import { usePageMeta } from '../hooks/usePageMeta.ts';
import { NotFoundPage } from './NotFoundPage.tsx';

export function ProjectPage() {
  const { slug } = useParams();
  const project = projects.find((item) => item.slug === slug);

  usePageMeta(
    project ? `${project.title} · Seth Stearns` : 'Page not found · Seth Stearns',
    project?.summary ?? 'That link does not match a page on Seth Stearns’s site.',
    project ? `/projects/${project.slug}` : '/',
  );

  if (!project) return <NotFoundPage />;

  return (
    <>
      <PageMain>
        <p>
          <a
            href="/#projects"
            className="text-accent text-sm font-semibold underline-offset-4 hover:underline"
          >
            <span aria-hidden="true">← </span>
            All projects
          </a>
        </p>
        <div className="mt-6">
          <StatusBadge status={project.status} />
        </div>
        <h1 className="text-ink mt-4 font-serif text-4xl font-semibold tracking-tight text-balance">
          {project.title}
        </h1>
        <p className="mt-4 text-lg leading-relaxed text-pretty">{project.summary}</p>
        {project.context ? (
          <p className="text-muted mt-4 leading-relaxed text-pretty">
            {project.context}
          </p>
        ) : null}
        <div className="mt-8">
          <ProjectCover project={project} />
        </div>
        <h2 className="text-muted mt-10 text-sm font-semibold tracking-[0.14em] uppercase">
          Highlights
        </h2>
        <ul className="marker:text-accent mt-4 list-disc space-y-2 pl-5 leading-relaxed">
          {project.highlights.map((highlight) => (
            <li key={highlight}>{highlight}</li>
          ))}
        </ul>
        <h2 className="text-muted mt-10 text-sm font-semibold tracking-[0.14em] uppercase">
          Technologies
        </h2>
        <div className="mt-4">
          <TechList items={project.tech} />
        </div>
        <div className="mt-8 flex flex-col items-start gap-2 text-sm">
          {project.demoUrl ? (
            <ExternalLink href={project.demoUrl}>Live demo</ExternalLink>
          ) : (
            <p className="text-muted">No public demo is listed.</p>
          )}
          {project.repoUrl ? (
            <ExternalLink href={project.repoUrl}>Source code</ExternalLink>
          ) : (
            <p className="text-muted">No public source repository is listed.</p>
          )}
        </div>
      </PageMain>
      <SiteFooter />
    </>
  );
}
