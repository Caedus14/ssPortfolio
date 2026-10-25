import type {
  Certification,
  EducationEntry,
  ExperienceEntry,
  Profile,
  Project,
  SectionNavItem,
  SkillGroup,
  SocialLink,
} from './types.ts';

/**
 * SITE CONTENT
 * ------------
 * Edit this file to change the words on the site. Page components read from
 * here so a wording change does not require a layout change.
 *
 * Every fact comes from Seth's resume. Do not add employers, metrics, dates,
 * or projects that are not listed. Do not add a phone number, street, or ZIP
 * code. The public location is city and state only.
 *
 * Entries marked PLACEHOLDER are real (the homelab, and this website) but
 * they are not client case studies. Say so plainly if you extend them.
 */

export const metaDescription =
  'Application developer building custom C# / .NET business applications, REST APIs, and Azure integrations. Based in Eau Claire, WI.';

export const siteTitle = 'Seth Stearns · Application Developer';

export const profile: Profile = {
  name: 'Seth Stearns',
  title: 'Application Developer',
  headline: 'C# / .NET · Azure · APIs & Integrations',
  email: 'seth.stearns@gmail.com',
  location: {
    city: 'Eau Claire',
    region: 'WI',
    country: 'US',
  },
  summary: [
    'Application Developer with 4+ years building custom business applications in C# / .NET on the Microsoft stack, and 10+ years in software development and IT.',
    'I design and consume RESTful APIs, integrate systems with third-party services, tune SQL Server for data-driven applications, and ship through Azure DevOps CI/CD.',
    'Most recently at Baker Tilly, a professional services firm, I built automation that saved thousands of work hours a year and modernized legacy applications to current .NET standards.',
    'Comfortable gathering requirements directly from stakeholders, owning deliverables end to end, and supporting applications once they are live.',
  ],
};

/** Public URLs. The portfolio repository is this project, not a second GitHub profile. */
export const links = {
  github: 'https://github.com/Caedus14',
  linkedin: 'https://www.linkedin.com/in/seth-stearns-a34950104',
  portfolioRepo: 'https://github.com/Caedus14/ssPortfolio',
} as const;

export const socialLinks: readonly SocialLink[] = [
  { id: 'github', label: 'GitHub', href: links.github },
  { id: 'linkedin', label: 'LinkedIn', href: links.linkedin },
  { id: 'email', label: 'Email', href: `mailto:${profile.email}` },
];

export const sections: readonly SectionNavItem[] = [
  { id: 'about', label: 'About', index: '01', heading: 'About' },
  { id: 'experience', label: 'Experience', index: '02', heading: 'Experience' },
  { id: 'skills', label: 'Skills', index: '03', heading: 'Skills' },
  { id: 'projects', label: 'Projects', index: '04', heading: 'Projects' },
  {
    id: 'education',
    label: 'Education',
    index: '05',
    heading: 'Education & Certifications',
  },
  { id: 'contact', label: 'Contact', index: '06', heading: 'Contact' },
];

export const sectionIds: readonly string[] = sections.map((section) => section.id);

export function sectionById(id: string): SectionNavItem {
  const section = sections.find((item) => item.id === id);
  if (!section) throw new Error(`Unknown section "${id}". Check src/data/content.ts.`);
  return section;
}

export const experience: readonly ExperienceEntry[] = [
  {
    id: 'baker-tilly',
    dates: '2022 – 2025',
    role: 'Software Developer',
    company: 'Baker Tilly',
    location: 'Madison, WI',
    highlights: [
      'Built and maintained cloud-connected business applications in C#, ASP.NET, and .NET 6 / .NET 8, used daily by staff at this professional services firm to run core operations.',
      'Designed, built, and consumed RESTful APIs to securely integrate internal applications with each other and with third-party services.',
      'Built automation tools that replaced slow manual processes, saving thousands of work hours each year and reducing human error.',
      'Built and maintained CI/CD pipelines in Azure DevOps and Azure, automating builds, testing, and deployments to ship updates faster with fewer errors.',
      'Optimized SQL Server queries, stored procedures, and schemas so data-driven applications stayed fast and reliable as data volume grew.',
      'Architected modular, maintainable application structures so new features could ship without costly full-system rewrites.',
      'Modernized legacy applications to current .NET standards and secure coding practices, improving security, performance, and long-term supportability.',
    ],
    tech: [
      'C#',
      'ASP.NET',
      '.NET 6',
      '.NET 8',
      'RESTful APIs',
      'SQL Server',
      'Azure DevOps',
      'Azure',
    ],
  },
  {
    id: 'group-health-cooperative',
    dates: '2021 – 2022',
    role: 'Software Developer',
    company: 'Group Health Cooperative',
    location: 'Eau Claire, WI',
    highlights: [
      'Built and shipped scalable C# / .NET applications on SQL Server across the full software delivery lifecycle at a regulated health insurer.',
      'Worked directly with stakeholders and cross-functional teams to gather requirements and turn them into reliable, working software.',
      'Delivered releases through Azure DevOps with thorough testing and debugging to meet strict quality and HIPAA compliance requirements.',
      'Supported and enhanced live systems, building internal tools that cut operational costs while keeping applications available.',
      'Worked in Agile / Scrum, delivering in short, predictable cycles and adapting quickly to change.',
    ],
    tech: ['C#', '.NET', 'SQL Server', 'Azure DevOps'],
  },
  {
    id: 'phillips-medisize',
    dates: '2014 – 2021',
    role: 'Development Specialist',
    company: 'Phillips-Medisize Corp. / Molex Inc.',
    location: 'Eau Claire, WI',
    highlights: [
      'Maintained, supported, and enhanced an intranet and internal web applications serving 1,000+ employees, modernizing legacy PHP and MySQL code to improve reliability and performance.',
      'Designed SharePoint sites and automated workflows that streamlined collaboration, document management, and business processes across the organization.',
      'Automated a company-wide data migration by scripting large-scale data extraction and transformation, eliminating hundreds of hours of manual work.',
      'Partnered with third-party developers to scope and deliver custom solutions on schedule and to requirements.',
      'Authored documentation and delivered one-on-one training to drive user adoption of new tools and systems.',
    ],
    tech: ['PHP', 'MySQL', 'SharePoint'],
  },
  {
    id: 'phillips-medisize-help-desk',
    dates: '2013',
    role: 'Help Desk Intern',
    company: 'Phillips-Medisize Corp. / Molex Inc.',
    location: 'Eau Claire, WI',
    highlights: [
      'Diagnosed and resolved hardware, software, networking, and ERP issues across departments, triaging and escalating support tickets to minimize downtime.',
    ],
    // The resume does not name products for this role. These words are the
    // domains in the bullet above. "Software" is left off because it is too vague.
    tech: ['Hardware', 'Networking', 'ERP'],
  },
];

export const skillGroups: readonly SkillGroup[] = [
  {
    id: 'languages',
    title: 'Languages',
    items: ['C#', 'SQL / T-SQL', 'JavaScript', 'VB.NET', 'Java', 'PHP', 'HTML', 'CSS'],
  },
  {
    id: 'microsoft',
    title: 'Microsoft & .NET',
    items: [
      '.NET 6',
      '.NET 8',
      'ASP.NET',
      'Microsoft SQL Server',
      'SharePoint',
      'Telerik',
    ],
  },
  {
    id: 'azure',
    title: 'Azure & DevOps',
    items: [
      'Microsoft Azure',
      'Azure DevOps',
      'CI/CD pipelines',
      'Git / source control',
    ],
  },
  {
    id: 'apis',
    title: 'APIs & Integration',
    items: [
      'RESTful API design and consumption',
      'third-party integrations',
      'process automation',
      'data migration',
    ],
  },
  {
    id: 'web',
    title: 'Web Front End',
    items: ['AngularJS', 'jQuery', 'Bootstrap', 'HTML5 / CSS3'],
  },
  {
    id: 'practices',
    title: 'Practices',
    items: [
      'OOP',
      'SDLC',
      'Agile / Scrum',
      'requirements gathering',
      'unit testing',
      'performance tuning',
      'secure development (TLS)',
    ],
  },
];

/**
 * Shown above the project cards. This is framing, not a new accomplishment.
 * Rewrite it when a public case study is added.
 */
export const projectsNote =
  'Public case studies are not listed yet. The entries below are a homelab I run and this website, which is still in progress.';

const coverSize = { width: 640, height: 360 } as const;

export const projects: readonly Project[] = [
  {
    slug: 'homelab',
    title: 'Self-hosted infrastructure',
    summary:
      'Self-hosted Linux servers and a home virtualization lab, including provisioning, networking, firewalls, security hardening, backups, monitoring, DNS, and secure remote access.',
    context:
      'Systems and DevOps work I build and maintain on my own time, using the same practices found in production environments.',
    highlights: [
      'Design, deploy, and administer self-hosted Linux servers and a home virtualization lab, handling provisioning, networking, firewalls, security hardening, automated backups, and monitoring.',
      'Manage DNS, reverse proxies, and secure remote access, independently troubleshooting performance, connectivity, and reliability issues.',
    ],
    // "live" means this lab is still running. It is not a public website.
    status: 'live',
    // Nouns taken from the resume bullets. Do not add product names (Proxmox,
    // Docker, Nginx, and so on) unless the resume names them.
    tech: [
      'Linux',
      'Virtualization',
      'Networking',
      'Firewalls',
      'DNS',
      'Reverse proxies',
      'Backups',
      'Monitoring',
    ],
    // PLACEHOLDER: no public demo. This is private home infrastructure.
    demoUrl: null,
    // PLACEHOLDER: no public source repository is listed on the resume.
    repoUrl: null,
    // PLACEHOLDER artwork, not a screenshot. alt is empty because it is decorative.
    cover: {
      lightSrc: '/images/homelab-light.svg',
      darkSrc: '/images/homelab-dark.svg',
      alt: '',
      ...coverSize,
    },
  },
  {
    // PLACEHOLDER / IN PROGRESS
    // This entry is the site itself, not a client project. There are no public
    // case-study projects on the resume yet. Do not add metrics or a fake demo URL.
    slug: 'portfolio',
    title: 'Personal portfolio',
    summary:
      'This website. A static resume site with project pages, a light and dark theme, and the wording kept in one typed file.',
    context: null,
    highlights: [
      'Phase 1 is the static site you are reading, built with React, TypeScript, and Vite.',
      'An ASP.NET Core contact API and an admin dashboard are planned later. They are not part of this version.',
    ],
    status: 'in-progress',
    tech: ['React', 'TypeScript', 'Vite', 'Tailwind CSS'],
    // PLACEHOLDER: no production URL until Azure Static Web Apps is configured.
    demoUrl: null,
    repoUrl: links.portfolioRepo,
    // PLACEHOLDER artwork, not a screenshot.
    cover: {
      lightSrc: '/images/portfolio-light.svg',
      darkSrc: '/images/portfolio-dark.svg',
      alt: '',
      ...coverSize,
    },
  },
];

export const education: readonly EducationEntry[] = [
  {
    id: 'wgu',
    credential: 'B.S., Software Development',
    school: 'Western Governors University',
    year: '2025',
  },
  {
    id: 'cvtc',
    credential: 'A.S., Software Development',
    school: 'Chippewa Valley Technical College',
    year: '2014',
  },
];

export const certifications: readonly Certification[] = [
  { id: 'comptia-a-plus', name: 'CompTIA A+', year: '2025' },
  {
    id: 'ciw-html',
    name: 'CIW Advanced HTML5 and CSS3 Specialist',
    year: '2020',
  },
  { id: 'ciw-ui', name: 'CIW User Interface Designer', year: '2020' },
];
