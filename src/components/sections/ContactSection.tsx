import { links, profile, sectionById } from '../../data/content.ts';
import { features } from '../../config/site.ts';
import { formatLocation } from '../../lib/format.ts';
import { buttonStyles } from '../buttonStyles.ts';
import { ContactForm } from '../contact/ContactForm.tsx';
import { ExternalLink } from '../ExternalLink.tsx';
import { ResumeLink } from '../ResumeLink.tsx';
import { Section } from '../Section.tsx';

const section = sectionById('contact');

/**
 * Email, LinkedIn, GitHub, and the resume stay available even when the form
 * is showing. There is no phone number and no street address.
 * `showForm` follows features.contactApi. Tests pass false to keep the
 * fallback-only section.
 */
export function ContactSection({
  showForm = features.contactApi,
}: {
  showForm?: boolean;
}) {
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
        <ExternalLink href={links.github} className={buttonStyles.secondary}>
          GitHub
        </ExternalLink>
        <ResumeLink />
      </div>
      {showForm ? <ContactForm /> : null}
    </Section>
  );
}
