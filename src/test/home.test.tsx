import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import * as content from '../data/content.ts';
import { features } from '../config/site.ts';
import { renderApp } from './render.tsx';

describe('home page', () => {
  it('shows the resume, landmarks, and a resume download', () => {
    renderApp('/');

    expect(
      screen.getByRole('heading', { level: 1, name: 'Seth Stearns' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /Experience/ })).toBeInTheDocument();
    expect(screen.getAllByText(/Baker Tilly/).length).toBeGreaterThan(0);
    expect(screen.getByText(/Group Health Cooperative/)).toBeInTheDocument();
    expect(screen.getByText(/Help Desk Intern/)).toBeInTheDocument();
    expect(screen.getByText(/saving thousands of work hours/)).toBeInTheDocument();
    expect(screen.getByText(/1,000\+ employees/)).toBeInTheDocument();
    expect(screen.getByText('Western Governors University')).toBeInTheDocument();
    expect(screen.getByText('CompTIA A+')).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'seth.stearns@gmail.com' }),
    ).toHaveAttribute('href', 'mailto:seth.stearns@gmail.com');
    expect(screen.getAllByRole('link', { name: /LinkedIn/ }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole('link', { name: /Download resume/ })[0]).toHaveAttribute(
      'href',
      '/resume.pdf',
    );
    expect(screen.getByRole('link', { name: 'Skip to content' })).toHaveAttribute(
      'href',
      '#main',
    );
    expect(screen.getByRole('navigation', { name: 'Sections' })).toBeInTheDocument();
    expect(screen.getByRole('main')).toBeInTheDocument();
    expect(screen.getByRole('banner')).toBeInTheDocument();
  });

  it('lazy-loads every content image', () => {
    renderApp('/');
    const images = document.querySelectorAll('img');
    expect(images.length).toBeGreaterThan(0);
    for (const image of images) {
      expect(image).toHaveAttribute('loading', 'lazy');
      expect(image).toHaveAttribute('decoding', 'async');
    }
  });

  it('does not publish a phone number, street, or ZIP code', () => {
    renderApp('/');
    const text = document.body.textContent ?? '';
    const serialized = JSON.stringify(content);

    expect(text).toContain('Eau Claire, WI');
    expect(text).not.toMatch(/\b\d{3}[-.)]\s*\d{3}[-.\s]\d{4}\b/);
    expect(text).not.toMatch(/\b\d{5}(?:-\d{4})?\b/);
    expect(text).not.toMatch(/\b(street|avenue|zip code)\b/i);
    expect(document.querySelector('a[href^="tel:"]')).toBeNull();
    expect(serialized).not.toMatch(/telephone|streetAddress|postalCode|"phone"/i);
    expect(features.contactApi).toBe(false);
  });
});
