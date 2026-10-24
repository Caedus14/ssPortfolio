import { links, profile, sectionById } from '../../data/content.ts';
import { formatLocation } from '../../lib/format.ts';
import { buttonStyles } from '../buttonStyles.ts';
import { ExternalLink } from '../ExternalLink.tsx';
import { ResumeLink } from '../ResumeLink.tsx';
import { Section } from '../Section.tsx';

const section = sectionById('contact');

/**
 * Phase 2 adds a form here when `features.contactApi` in src/config/site.ts
 * is turned on. Until then the only actions are links. There is no phone number.
 */
export function ContactSection() {
  return (
    <Section id={section.id} index={section.index} title={section.heading}>
      <p className="text-ink text-lg">{formatLocation(profile.location)}</p>
      <div className="mt-6 flex flex-col items-start gap-3 sm:flex-row sm:flex-wrap">
        <a className={buttonStyles.primary} href={`mailto:${profile.email}`}>
          {profile.email}
        </a>
        <ExternalLink href={links.linkedin} className={buttonStyles.secondary}>
          LinkedIn
        </ExternalLink>
        <ResumeLink />
      </div>
    </Section>
  );
}
