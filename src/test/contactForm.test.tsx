import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { TurnstileRenderOptions } from '../turnstile.ts';
import { ContactForm } from '../components/contact/ContactForm.tsx';
import { ContactSection } from '../components/sections/ContactSection.tsx';
import { contactMessages } from '../lib/contactValidation.ts';
import { renderWithTheme } from './render.tsx';

type TurnstileMock = {
  render: (element: HTMLElement, options: TurnstileRenderOptions) => string;
  reset: (id?: string) => void;
  remove: (id?: string) => void;
};

function installObserver() {
  class ImmediateObserver implements IntersectionObserver {
    readonly root: Element | Document | null = null;
    readonly rootMargin = '';
    readonly scrollMargin = '';
    readonly thresholds: readonly number[] = [];
    private readonly callback: IntersectionObserverCallback;
    constructor(callback: IntersectionObserverCallback) {
      this.callback = callback;
    }
    observe() {
      this.callback([{ isIntersecting: true } as IntersectionObserverEntry], this);
    }
    unobserve() {}
    disconnect() {}
    takeRecords(): IntersectionObserverEntry[] {
      return [];
    }
  }
  vi.stubGlobal('IntersectionObserver', ImmediateObserver);
}

function tokenFrom(ms: number): string {
  return `${btoa(String(ms)).replace(/=+$/g, '')}.sig`;
}

function jsonResponse(status: number, body: unknown, headers?: Record<string, string>) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json', ...headers },
  });
}

describe('contact form', () => {
  let turnstile: TurnstileMock;
  const sleeps: number[] = [];

  beforeEach(() => {
    sleeps.length = 0;
    installObserver();
    turnstile = {
      render: (_element, options) => {
        options.callback?.('turnstile-1');
        turnstileRender = options;
        return 'widget-1';
      },
      reset: () => {
        turnstileRender?.callback?.('turnstile-2');
      },
      remove: () => {},
    };
    window.turnstile = turnstile;
  });

  let turnstileRender: TurnstileRenderOptions | null = null;

  afterEach(() => {
    cleanup();
    delete window.turnstile;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    turnstileRender = null;
  });

  function renderForm(now = () => Date.UTC(2026, 10, 3, 12, 0, 0)) {
    return renderWithTheme(
      <ContactForm
        now={now}
        sleep={async (ms) => {
          sleeps.push(ms);
        }}
      />,
    );
  }

  async function fillValid() {
    await waitFor(() => expect(turnstileRender).not.toBeNull());
    fireEvent.change(screen.getByLabelText('Name'), {
      target: { value: 'Ada Lovelace' },
    });
    fireEvent.change(screen.getByLabelText('Email'), {
      target: { value: 'ada@example.com' },
    });
    fireEvent.change(screen.getByLabelText('Message'), {
      target: { value: 'Hello from the form.' },
    });
  }

  function mockFetch(
    respond: (
      url: string,
      init: RequestInit | undefined,
      call: number,
    ) => Response | Promise<Response>,
  ) {
    let posts = 0;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.endsWith('/api/contact') && init?.method === 'POST') {
        posts += 1;
        return respond(url, init, posts);
      }
      return jsonResponse(200, {
        formToken: tokenFrom(Date.UTC(2026, 10, 3, 12, 0, 0)),
      });
    });
    vi.stubGlobal('fetch', fetchMock);
    return { fetchMock, posts: () => posts };
  }

  it('exposes labels, limits, and a honeypot that is off-screen', async () => {
    mockFetch(() => jsonResponse(200, { status: 'accepted' }));
    renderForm();

    expect(screen.getByLabelText('Name')).toHaveAttribute('autocomplete', 'name');
    expect(screen.getByLabelText('Name')).toHaveAttribute('maxlength', '100');
    expect(screen.getByLabelText('Email')).toHaveAttribute('autocomplete', 'email');
    expect(screen.getByLabelText('Email')).toHaveAttribute('maxlength', '254');
    expect(screen.getByLabelText('Message')).toHaveAttribute('maxlength', '2000');
    expect(screen.getByText('0 / 2000')).toBeInTheDocument();

    const honeypot = document.getElementById('contact-website');
    expect(honeypot).toHaveAttribute('tabindex', '-1');
    expect(honeypot).toHaveAttribute('autocomplete', 'off');
    expect(honeypot?.parentElement).toHaveAttribute('aria-hidden', 'true');
    expect(honeypot?.parentElement).toHaveClass('contact-honeypot');
    expect(honeypot?.parentElement?.className).not.toContain('hidden');

    expect(
      screen.getByRole('link', { name: "Cloudflare's privacy policy" }),
    ).toHaveAttribute('href', 'https://www.cloudflare.com/privacypolicy/');
    await screen.findByRole('button', { name: 'Send message' });
  });

  it('focuses the first invalid field and links the error', async () => {
    mockFetch(() => jsonResponse(200, { status: 'accepted' }));
    renderForm();
    fireEvent.click(screen.getByRole('button', { name: 'Send message' }));

    const name = screen.getByLabelText('Name');
    expect(name).toHaveFocus();
    expect(name).toHaveAttribute('aria-invalid', 'true');
    expect(name).toHaveAttribute('aria-describedby', 'contact-name-error');
    expect(screen.getByText(contactMessages.nameLength)).toBeInTheDocument();
    expect(screen.getByLabelText('Message')).toHaveAttribute(
      'aria-describedby',
      expect.stringContaining('contact-message-error'),
    );
  });

  it('thanks the person and clears the fields on 200', async () => {
    const { fetchMock } = mockFetch(() => jsonResponse(200, { status: 'accepted' }));
    renderForm();
    await fillValid();
    fireEvent.click(screen.getByRole('button', { name: 'Send message' }));

    expect(
      await screen.findByText('Thank you. Your message is on its way.'),
    ).toBeInTheDocument();
    expect(screen.getByLabelText('Name')).toHaveValue('');
    expect(screen.getByLabelText('Message')).toHaveValue('');
    const post = fetchMock.mock.calls.find((call) => {
      const init = call[1] as RequestInit | undefined;
      return init?.method === 'POST';
    });
    const body = JSON.parse(String((post?.[1] as RequestInit).body)) as {
      formToken: string;
      turnstileToken: string;
      website: string;
    };
    expect(body.turnstileToken).toBe('turnstile-1');
    expect(body.formToken).toContain('.sig');
    expect(body.website).toBe('');
  });

  it('shows server field errors inline', async () => {
    mockFetch(() =>
      jsonResponse(400, {
        error: 'invalid',
        fields: { email: contactMessages.email, name: contactMessages.nameLength },
      }),
    );
    renderForm();
    await fillValid();
    fireEvent.click(screen.getByRole('button', { name: 'Send message' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Check the fields above',
    );
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText(contactMessages.email)).toBeInTheDocument();
  });

  it('refreshes an expired form token and asks for another try', async () => {
    const { fetchMock } = mockFetch(() =>
      jsonResponse(400, {
        error: 'form_token_expired',
        fields: { formToken: 'Reload' },
      }),
    );
    renderForm();
    await fillValid();
    fireEvent.click(screen.getByRole('button', { name: 'Send message' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('I refreshed it');
    const tokenGets = fetchMock.mock.calls.filter((call) =>
      String(call[0]).endsWith('/api/contact/token'),
    );
    expect(tokenGets.length).toBeGreaterThanOrEqual(2);
    const posts = fetchMock.mock.calls.filter(
      (call) => (call[1] as RequestInit)?.method === 'POST',
    );
    expect(posts).toHaveLength(1);
  });

  it('refreshes a token older than 1 hour 50 minutes before sending', async () => {
    const issued = Date.UTC(2026, 10, 3, 12, 0, 0);
    const { fetchMock } = mockFetch((url) => {
      if (url.endsWith('/api/contact/token')) {
        return jsonResponse(200, { formToken: tokenFrom(issued) });
      }
      return jsonResponse(200, { status: 'accepted' });
    });
    renderForm(() => issued + 110 * 60 * 1000);
    await fillValid();
    fireEvent.click(screen.getByRole('button', { name: 'Send message' }));
    await screen.findByText('Thank you. Your message is on its way.');

    const tokenGets = fetchMock.mock.calls.filter((call) =>
      String(call[0]).endsWith('/api/contact/token'),
    );
    expect(tokenGets.length).toBeGreaterThanOrEqual(2);
  });

  it('explains a rate limit with the server wait', async () => {
    mockFetch(() =>
      jsonResponse(429, { error: 'rate_limited', retryAfterSeconds: 30 }),
    );
    renderForm();
    await fillValid();
    fireEvent.click(screen.getByRole('button', { name: 'Send message' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('wait 30 seconds');
  });

  it('retries a 503 once, then offers email', async () => {
    let releaseSleep: () => void = () => {};
    const { posts } = mockFetch(() =>
      jsonResponse(
        503,
        { error: 'unavailable', retryAfterSeconds: 2 },
        { 'Retry-After': '2' },
      ),
    );
    renderWithTheme(
      <ContactForm
        now={() => Date.UTC(2026, 10, 3, 12, 0, 0)}
        sleep={(ms) =>
          new Promise((resolve) => {
            sleeps.push(ms);
            releaseSleep = resolve;
          })
        }
      />,
    );
    await fillValid();
    fireEvent.click(screen.getByRole('button', { name: 'Send message' }));

    expect(
      await screen.findByText('Waking up the server, one moment.'),
    ).toBeInTheDocument();
    expect(sleeps).toContain(2000);
    releaseSleep();
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'could not send just now',
    );
    expect(screen.getByRole('link', { name: 'email me' })).toHaveAttribute(
      'href',
      'mailto:seth.stearns@gmail.com',
    );
    await waitFor(() => expect(posts()).toBe(2));
  });

  it('shows a friendly message when the token endpoint is unavailable', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => jsonResponse(503, { error: 'form_token_unavailable' })),
    );
    renderForm();
    expect(await screen.findByRole('alert')).toHaveTextContent('not ready yet');
    expect(screen.getByRole('link', { name: 'email me' })).toBeInTheDocument();
  });

  it('uses a generic message for an unexpected status', async () => {
    mockFetch(() => jsonResponse(415, { error: 'unsupported_media_type' }));
    renderForm();
    await fillValid();
    fireEvent.click(screen.getByRole('button', { name: 'Send message' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong');
  });

  it('ignores a second click while the first send is in flight', async () => {
    let release: (response: Response) => void = () => {};
    let posts = 0;
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        if (String(input).endsWith('/api/contact') && init?.method === 'POST') {
          posts += 1;
          return new Promise<Response>((resolve) => {
            release = resolve;
          });
        }
        return Promise.resolve(
          jsonResponse(200, { formToken: tokenFrom(Date.UTC(2026, 10, 3, 12, 0, 0)) }),
        );
      }),
    );
    renderForm();
    await fillValid();
    const button = screen.getByRole('button', { name: 'Send message' });
    fireEvent.click(button);
    fireEvent.click(button);
    await waitFor(() => expect(posts).toBe(1));
    expect(button).toBeDisabled();
    release(jsonResponse(200, { status: 'accepted' }));
    expect(
      await screen.findByText('Thank you. Your message is on its way.'),
    ).toBeInTheDocument();
    expect(posts).toBe(1);
  });

  it('says to use email when the Turnstile script is blocked', async () => {
    delete window.turnstile;
    vi.spyOn(document.head, 'appendChild').mockImplementation((node) => {
      if (node instanceof HTMLScriptElement && node.src.includes('turnstile')) {
        queueMicrotask(() => node.dispatchEvent(new Event('error')));
      }
      return node;
    });
    renderForm();
    expect(await screen.findByText(/could not load/)).toBeInTheDocument();
  });

  it('keeps the fallback links when the form flag is off', () => {
    render(<ContactSection showForm={false} />);
    expect(screen.queryByLabelText('Message')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: /LinkedIn/ })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /GitHub/ })).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'seth.stearns@gmail.com' }),
    ).toBeInTheDocument();
  });
});
