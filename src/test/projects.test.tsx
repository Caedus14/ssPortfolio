import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import userEvent from '@testing-library/user-event';
import { StatusBadge } from '../components/StatusBadge.tsx';
import { pageUrl } from '../config/site.ts';
import { links } from '../data/content.ts';
import { renderApp } from './render.tsx';

describe('projects', () => {
  it('filters cards by a technology tag and can show them all again', async () => {
    const user = userEvent.setup();
    renderApp('/');

    expect(
      screen.getByRole('link', { name: /Self-hosted infrastructure/ }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'React' }));

    expect(
      screen.queryByRole('link', { name: /Self-hosted infrastructure/ }),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: /Personal portfolio/ }),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'React' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(screen.getByRole('button', { name: 'All' })).toHaveAttribute(
      'aria-pressed',
      'false',
    );

    await user.click(screen.getByRole('button', { name: 'All' }));
    expect(
      screen.getByRole('link', { name: /Self-hosted infrastructure/ }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: /Personal portfolio/ }),
    ).toBeInTheDocument();
  });

  it('describes this site as in progress, with source and no demo', () => {
    renderApp('/projects/portfolio');
    expect(
      screen.getByRole('heading', { level: 1, name: 'Personal portfolio' }),
    ).toBeInTheDocument();
    expect(screen.getByText('In progress')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Source code/ })).toHaveAttribute(
      'href',
      links.portfolioRepo,
    );
    expect(screen.queryByRole('link', { name: /Live demo/ })).not.toBeInTheDocument();
    expect(screen.getByText('No public demo is listed.')).toBeInTheDocument();
    expect(document.querySelector('link[rel="canonical"]')).toHaveAttribute(
      'href',
      pageUrl('/projects/portfolio'),
    );
    expect(document.querySelector('meta[property="og:url"]')).toHaveAttribute(
      'content',
      pageUrl('/projects/portfolio'),
    );
  });

  it('keeps the homelab private', () => {
    renderApp('/projects/homelab');
    expect(
      screen.getByRole('heading', { level: 1, name: 'Self-hosted infrastructure' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Live')).toBeInTheDocument();
    expect(
      screen.getByText(/Design, deploy, and administer self-hosted Linux servers/),
    ).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Source code/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Live demo/ })).not.toBeInTheDocument();
    expect(
      screen.getByText('No public source repository is listed.'),
    ).toBeInTheDocument();
    expect(screen.getByText('No public demo is listed.')).toBeInTheDocument();
    expect(document.querySelector('link[rel="canonical"]')).toHaveAttribute(
      'href',
      pageUrl('/projects/homelab'),
    );
  });

  it('explains an unknown project address', () => {
    renderApp('/projects/not-a-real-project');
    expect(
      screen.getByRole('heading', { level: 1, name: 'Page not found' }),
    ).toBeInTheDocument();
  });

  it('has a label ready for an archived project', () => {
    render(<StatusBadge status="archived" />);
    expect(screen.getByText('Archived')).toBeInTheDocument();
  });
});
