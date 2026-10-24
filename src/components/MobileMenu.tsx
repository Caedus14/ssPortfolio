import { useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { IconClose } from './icons.tsx';
import { ResumeLink } from './ResumeLink.tsx';
import { SectionNav } from './SectionNav.tsx';
import { SocialLinks } from './SocialLinks.tsx';

/**
 * A native `<dialog>` traps focus and closes on Escape in real browsers.
 * jsdom does not implement showModal, so tests fall back to the open attribute.
 * The menu is mounted only while it is open, which keeps the closed links
 * out of the tab order.
 */
export function MobileMenu({
  current,
  onClose,
}: {
  current: string | null;
  onClose: () => void;
}) {
  const ref = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;

    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    const onBackdrop = (event: MouseEvent) => {
      if (event.target === dialog) onClose();
    };

    document.addEventListener('keydown', onKey);
    dialog.addEventListener('click', onBackdrop);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    if (typeof dialog.showModal === 'function') {
      dialog.showModal();
    } else {
      dialog.setAttribute('open', '');
    }

    return () => {
      document.removeEventListener('keydown', onKey);
      dialog.removeEventListener('click', onBackdrop);
      document.body.style.overflow = previousOverflow;
      if (typeof dialog.close === 'function' && dialog.open) dialog.close();
    };
  }, [onClose]);

  return createPortal(
    <dialog
      ref={ref}
      id="mobile-menu"
      aria-labelledby="mobile-menu-title"
      className="border-line bg-canvas text-ink fixed inset-y-0 right-0 z-50 m-0 h-dvh max-h-none w-[min(100%,22rem)] border-y-0 border-r-0 border-l p-6"
    >
      <div className="flex items-center justify-between gap-4">
        <p id="mobile-menu-title" className="text-lg font-semibold">
          Menu
        </p>
        <button
          type="button"
          className="border-line inline-flex size-11 items-center justify-center rounded-full border"
          aria-label="Close menu"
          onClick={onClose}
        >
          <IconClose />
        </button>
      </div>
      <div className="mt-6">
        <SectionNav current={current} onNavigate={onClose} variant="menu" />
      </div>
      <div className="mt-8 flex flex-col items-start gap-5">
        <ResumeLink />
        <SocialLinks />
      </div>
    </dialog>,
    document.body,
  );
}
