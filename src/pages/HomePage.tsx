import { AboutSection } from '../components/sections/AboutSection.tsx';
import { ContactSection } from '../components/sections/ContactSection.tsx';
import { EducationSection } from '../components/sections/EducationSection.tsx';
import { ExperienceSection } from '../components/sections/ExperienceSection.tsx';
import { ProjectsSection } from '../components/sections/ProjectsSection.tsx';
import { SkillsSection } from '../components/sections/SkillsSection.tsx';
import { PageMain } from '../components/PageMain.tsx';
import { SiteFooter } from '../components/SiteFooter.tsx';
import { metaDescription, siteTitle } from '../data/content.ts';
import { usePageMeta } from '../hooks/usePageMeta.ts';

export function HomePage() {
  usePageMeta(siteTitle, metaDescription, '/');

  return (
    <>
      <PageMain>
        <AboutSection />
        <ExperienceSection />
        <SkillsSection />
        <ProjectsSection />
        <EducationSection />
        <ContactSection />
      </PageMain>
      <SiteFooter />
    </>
  );
}
