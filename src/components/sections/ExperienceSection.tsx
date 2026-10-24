import { experience, sectionById } from '../../data/content.ts';
import { Section } from '../Section.tsx';
import { TechList } from '../TechList.tsx';

const section = sectionById('experience');

export function ExperienceSection() {
  return (
    <Section id={section.id} index={section.index} title={section.heading}>
      <div className="divide-line divide-y">
        {experience.map((job) => (
          <article
            key={job.id}
            className="grid gap-3 py-8 first:pt-0 sm:grid-cols-[9.5rem_minmax(0,1fr)] sm:gap-8"
          >
            <div>
              <p className="text-muted font-mono text-xs tracking-wide uppercase">
                {job.dates}
              </p>
              <p className="text-muted mt-1 text-sm">{job.location}</p>
            </div>
            <div>
              <h3 className="text-ink text-lg font-semibold tracking-tight">
                {job.role}
                <span className="text-muted font-normal"> · {job.company}</span>
              </h3>
              <ul className="marker:text-accent mt-4 list-disc space-y-2 pl-5 leading-relaxed">
                {job.highlights.map((highlight) => (
                  <li key={highlight}>{highlight}</li>
                ))}
              </ul>
              <div className="mt-4">
                <TechList items={job.tech} />
              </div>
            </div>
          </article>
        ))}
      </div>
    </Section>
  );
}
