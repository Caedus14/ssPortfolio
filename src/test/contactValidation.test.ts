import { describe, expect, it } from 'vitest';
import {
  formTokenIssuedAt,
  formTokenNeedsRefresh,
  wakeWaitMs,
} from '../lib/contactApi.ts';
import {
  contactMessages,
  containsHtmlTag,
  countLinks,
  validateContact,
} from '../lib/contactValidation.ts';

describe('contact validation', () => {
  it('accepts a normal note and keeps a less-than sign that is not a tag', () => {
    expect(
      validateContact({
        name: 'Ada Lovelace',
        email: 'Ada@Example.COM',
        message: 'Hello there. 5 < 10 is fine.',
      }),
    ).toEqual({});
  });

  it('mirrors the API field messages', () => {
    expect(validateContact({ name: ' ', email: 'nope', message: 'short' })).toEqual({
      name: contactMessages.nameLength,
      email: contactMessages.email,
      message: contactMessages.messageLength,
    });
    expect(
      validateContact({
        name: 'Ada\nLovelace',
        email: 'ada@example.com',
        message: 'Hello there.',
      }),
    ).toEqual({
      name: contactMessages.nameControl,
    });
    expect(
      validateContact({
        name: 'Ada',
        email: 'ada@example.com',
        message: 'Hello <b>there</b> friend.',
      }),
    ).toEqual({ message: contactMessages.messageHtml });
    expect(
      validateContact({
        name: 'Ada',
        email: 'ada@example.com',
        message: 'See https://a.example https://b.example and www.c.example please.',
      }),
    ).toEqual({ message: contactMessages.messageLinks });
  });

  it('counts https://www as one link and allows two', () => {
    expect(countLinks('See https://www.example.com today.')).toBe(1);
    expect(
      validateContact({
        name: 'Ada',
        email: 'ada@example.com',
        message: 'See https://a.example and https://b.example please.',
      }),
    ).toEqual({});
  });

  it('rejects a tag and allows a spaced less-than', () => {
    expect(containsHtmlTag('hello <script')).toBe(true);
    expect(containsHtmlTag('a < b is fine!!')).toBe(false);
  });
});

describe('form token age', () => {
  it('reads the issued time and asks for a refresh after 1 hour 50 minutes', () => {
    const issued = Date.UTC(2026, 10, 3, 12, 0, 0);
    const token = `${btoa(String(issued)).replace(/=+$/g, '')}.sig`;
    expect(formTokenIssuedAt(token)).toBe(issued);
    expect(formTokenNeedsRefresh(token, issued + 110 * 60 * 1000 - 1)).toBe(false);
    expect(formTokenNeedsRefresh(token, issued + 110 * 60 * 1000)).toBe(true);
  });

  it('caps the wake-up wait at 15 seconds', () => {
    expect(wakeWaitMs(2, null)).toBe(2000);
    expect(wakeWaitMs(undefined, '30')).toBe(15_000);
    expect(wakeWaitMs(undefined, null)).toBe(1500);
  });
});
