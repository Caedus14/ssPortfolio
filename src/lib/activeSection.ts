export type SectionPosition = {
  id: string;
  /** Distance from the top of the document, in pixels. */
  top: number;
};

/**
 * The scroll spy draws a horizontal marker about a third of the way down
 * the viewport. The active section is the last one whose top has crossed
 * that line. The first section wins when the page is still at the top.
 */
export function pickActiveSection(
  sections: readonly SectionPosition[],
  marker: number,
): string {
  let current = sections[0]?.id ?? '';
  for (const section of sections) {
    if (section.top <= marker) current = section.id;
  }
  return current;
}
