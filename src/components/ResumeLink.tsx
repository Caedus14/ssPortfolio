import { cn } from '../lib/cn.ts';
import { buttonStyles } from './buttonStyles.ts';
import { IconDownload } from './icons.tsx';

/**
 * Points at /resume.pdf. That file is not in git — a PDF export often still
 * contains a phone number or street address. See the README before adding one.
 */
export function ResumeLink({ className }: { className?: string }) {
  return (
    <a
      href="/resume.pdf"
      download="Seth-Stearns-Resume.pdf"
      className={cn(buttonStyles.secondary, className)}
    >
      <IconDownload className="size-4" />
      Download resume
    </a>
  );
}
