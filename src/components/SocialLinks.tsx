import { socialLinks } from '../data/content.ts';
import type { SocialLink } from '../data/types.ts';
import { IconGithub, IconLinkedIn, IconMail } from './icons.tsx';

const icons: Record<SocialLink['id'], typeof IconGithub> = {
  github: IconGithub,
  linkedin: IconLinkedIn,
  email: IconMail,
};

export function SocialLinks() {
  return (
    <ul className="flex list-none items-center gap-1 p-0">
      {socialLinks.map((link) => {
        const Icon = icons[link.id];
        const external = !link.href.startsWith('mailto:');
        return (
          <li key={link.id}>
            <a
              href={link.href}
              className="text-muted hover:text-accent inline-flex size-11 items-center justify-center rounded-full motion-safe:transition-colors"
              {...(external ? { target: '_blank', rel: 'noreferrer noopener' } : {})}
            >
              <Icon />
              <span className="sr-only">
                {link.label}
                {external ? ' (opens in a new tab)' : ''}
              </span>
            </a>
          </li>
        );
      })}
    </ul>
  );
}
