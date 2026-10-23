import { certifications, education, sectionById } from '../../data/content.ts';
import { Section } from '../Section.tsx';

const section = sectionById('education');

export function EducationSection() {
  return (
    <Section id={section.id} index={section.index} title={section.heading}>
      <ol className="border-line m-0 list-none space-y-8 border-l p-0 pl-6">
        {education.map((item) => (
          <li key={item.id} className="relative">
            <span
              className="bg-accent ring-canvas absolute top-1.5 -left-[1.95rem] size-2.5 rounded-full ring-4"
              aria-hidden="true"
            />
            <p className="text-muted font-mono text-xs tracking-wide uppercase">
              {item.year}
            </p>
            <h3 className="text-ink mt-1 text-lg font-semibold">{item.credential}</h3>
            <p className="text-muted">{item.school}</p>
          </li>
        ))}
      </ol>
      <h3 className="text-ink mt-12 text-lg font-semibold">Certifications</h3>
      <ul className="mt-4 list-none space-y-3 p-0">
        {certifications.map((item) => (
          <li
            key={item.id}
            className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1"
          >
            <span className="text-ink font-medium">{item.name}</span>
            <span className="text-muted font-mono text-xs tracking-wide uppercase">
              {item.year}
            </span>
          </li>
        ))}
      </ul>
    </Section>
  );
}
