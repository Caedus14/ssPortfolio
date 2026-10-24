import { profile, sectionById } from '../../data/content.ts';
import { Section } from '../Section.tsx';

const about = sectionById('about');

export function AboutSection() {
  return (
    <Section id={about.id} index={about.index} title={about.heading} spacing="first">
      <div className="space-y-4 text-pretty">
        {profile.summary.map((paragraph, index) => (
          <p
            key={paragraph}
            className={index === 0 ? 'text-lg leading-relaxed' : 'leading-relaxed'}
          >
            {paragraph}
          </p>
        ))}
      </div>
    </Section>
  );
}
