import { motion, useReducedMotion } from 'framer-motion';
import type { ReactNode } from 'react';

/**
 * A short slide when a section enters the viewport. Opacity stays at 1 so
 * the largest text is not invisible while an animation waits to start.
 * `useReducedMotion` is true when the visitor asked the OS for less motion.
 */
export function Reveal({
  children,
  className,
  delay = 0,
}: {
  children: ReactNode;
  className?: string;
  delay?: number;
}) {
  const reduce = useReducedMotion();
  if (reduce) return <div className={className}>{children}</div>;

  return (
    <motion.div
      className={className}
      initial={{ y: 10 }}
      whileInView={{ y: 0 }}
      viewport={{ once: true, margin: '0px 0px -8% 0px' }}
      transition={{ duration: 0.4, delay, ease: 'easeOut' }}
    >
      {children}
    </motion.div>
  );
}
