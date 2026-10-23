/**
 * Shapes for everything in src/data/content.ts.
 * Components should accept these types and not invent fields of their own.
 * Phase 3 (an admin dashboard) should edit these same shapes.
 */

export type Location = {
  /** City only. Do not add a street. */
  city: string;
  /** State or region. Do not add a postal code. */
  region: string;
  country: string;
};

export type Profile = {
  name: string;
  title: string;
  /** Short specialty line under the title. */
  headline: string;
  email: string;
  location: Location;
  /** Resume summary, one sentence per entry, in the original order. */
  summary: readonly string[];
};

export type SocialLink = {
  id: 'github' | 'linkedin' | 'email';
  label: string;
  href: string;
};

export type SectionNavItem = {
  id: string;
  /** Short label for the side navigation. */
  label: string;
  /** Two-digit index shown next to the heading, for example "01". */
  index: string;
  /** Visible heading. Can be longer than the nav label. */
  heading: string;
};

export type ExperienceEntry = {
  id: string;
  /** Display text, not a parsed date. Example: "2022 – 2025". */
  dates: string;
  role: string;
  company: string;
  /** City and state only. */
  location: string;
  highlights: readonly string[];
  /**
   * Tags named by this role's bullets. Do not add tools that are not written there.
   */
  tech: readonly string[];
};

export type SkillGroup = {
  id: string;
  title: string;
  items: readonly string[];
};

/** live = still running, in-progress = being built, archived = no longer active. */
export type ProjectStatus = 'live' | 'in-progress' | 'archived';

export type ProjectCover = {
  lightSrc: string;
  darkSrc: string;
  /**
   * Empty string when the image is decorative.
   * A real screenshot needs a description of what is in the picture.
   */
  alt: string;
  width: number;
  height: number;
};

export type Project = {
  slug: string;
  title: string;
  /** Short card text. Keep it to facts already in `highlights` or `context`. */
  summary: string;
  /**
   * Optional sentence shown on the detail page.
   * Use this for a line copied from the resume that does not fit the card.
   */
  context: string | null;
  highlights: readonly string[];
  status: ProjectStatus;
  tech: readonly string[];
  /** Public demo. Null when there is no real URL — do not invent one. */
  demoUrl: string | null;
  /** Public source repository. Null when the work is private. */
  repoUrl: string | null;
  /** Null hides the figure. Decorative covers use alt: "". */
  cover: ProjectCover | null;
};

export type EducationEntry = {
  id: string;
  credential: string;
  school: string;
  year: string;
};

export type Certification = {
  id: string;
  name: string;
  year: string;
};
