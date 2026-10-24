import { sectionById, skillGroups } from '../../data/content.ts';
import { Section } from '../Section.tsx';
import { TechList } from '../TechList.tsx';

const section = sectionById('skills');

export function SkillsSection() {
  return (
    <Section id={section.id} index={section.index} title={section.heading}>
      <ul className="grid list-none gap-4 p-0 sm:grid-cols-2">
        {skillGroups.map((group) => (
          <li
            key={group.id}
            className="border-line bg-surface shadow-card rounded-2xl border p-5"
          >
            <h3 className="text-ink text-base font-semibold">{group.title}</h3>
            <div className="mt-4">
              <TechList items={group.items} />
            </div>
          </li>
        ))}
      </ul>
    </Section>
  );
}
