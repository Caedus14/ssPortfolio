import { cn } from '../lib/cn.ts';
import { buttonStyles } from './buttonStyles.ts';
import { IconDownload } from './icons.tsx';

/**
 * Same-origin file in public/resume.pdf. The download name is the one the
 * browser should save. Azure also sends that name via Content-Disposition.
 * The committed PDF must stay phone-free. See the README.
 */
export function ResumeLink({ className }: { className?: string }) {
  return (
    <a
      href="/resume.pdf"
      download="Seth_Stearns_Resume.pdf"
      className={cn(buttonStyles.secondary, className)}
    >
      <IconDownload className="size-4" />
      Download resume
    </a>
  );
}
