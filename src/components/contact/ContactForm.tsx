import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { profile } from '../../data/content.ts';
import {
  classifyContactResponse,
  contactEndpoint,
  formTokenNeedsRefresh,
  type ContactOutcome,
} from '../../lib/contactApi.ts';
import {
  MESSAGE_MAX,
  NAME_MAX,
  EMAIL_MAX,
  validateContact,
  type ContactField,
  type ContactFieldErrors,
} from '../../lib/contactValidation.ts';
import { turnstileAction, turnstileSiteKey } from '../../config/site.ts';
import { useTheme } from '../../theme/theme-context.ts';
import { buttonStyles } from '../buttonStyles.ts';
import { ensureTurnstileScript } from './turnstileScript.ts';

const fieldOrder: readonly ContactField[] = ['name', 'email', 'message'];

type Status =
  | { tone: 'polite'; text: string }
  | { tone: 'alert'; text: string }
  | { tone: 'alert'; emailFallback: 'ready' | 'send' };

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => {
    window.setTimeout(resolve, ms);
  });
}

function clockNow(): number {
  return Date.now();
}

const refreshed = 'That check expired. I refreshed it. Please send the message again.';
const generic = 'Something went wrong sending the message. Try again, or email me.';
const blocked = 'The bot check could not load. You can email me instead.';
const needCheck = 'Complete the check above, then send the message.';
const waking = 'Waking up the server, one moment.';

export function ContactForm({
  sleep = delay,
  now = clockNow,
}: {
  sleep?: (ms: number) => Promise<void>;
  now?: () => number;
}) {
  const { theme } = useTheme();
  const rootRef = useRef<HTMLDivElement>(null);
  const widgetRef = useRef<HTMLDivElement>(null);
  const widgetIdRef = useRef<string | undefined>(undefined);
  const turnstileRef = useRef('');
  const sendingRef = useRef(false);
  const ageRefreshRef = useRef(false);
  const [near, setNear] = useState(false);
  const [sending, setSending] = useState(false);
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [message, setMessage] = useState('');
  const [website, setWebsite] = useState('');
  const [errors, setErrors] = useState<ContactFieldErrors>({});
  const [status, setStatus] = useState<Status | null>(null);
  const [formToken, setFormToken] = useState<string | null>(null);
  const [widgetBlocked, setWidgetBlocked] = useState(false);

  useEffect(() => {
    const node = rootRef.current;
    if (!node || typeof IntersectionObserver === 'undefined') {
      setNear(true);
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          setNear(true);
          observer.disconnect();
        }
      },
      { rootMargin: '240px' },
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (!near) return;
    let cancel = false;
    void requestFormToken().then((token) => {
      if (cancel) return;
      if (token) setFormToken(token);
      else setStatus({ tone: 'alert', emailFallback: 'ready' });
    });
    return () => {
      cancel = true;
    };
  }, [near]);

  useEffect(() => {
    if (!formToken) return;
    const id = window.setInterval(() => {
      if (ageRefreshRef.current) return;
      if (!formTokenNeedsRefresh(formToken, now())) return;
      ageRefreshRef.current = true;
      void requestFormToken().then((token) => {
        if (token) setFormToken(token);
        else ageRefreshRef.current = false;
      });
    }, 60_000);
    return () => window.clearInterval(id);
  }, [formToken, now]);

  useEffect(() => {
    if (!near) return;
    const node = widgetRef.current;
    if (!node) return;
    let cancel = false;
    let widgetId: string | undefined;
    void ensureTurnstileScript().then((result) => {
      if (cancel) return;
      if (result === 'blocked' || !window.turnstile) {
        setWidgetBlocked(true);
        return;
      }
      widgetId = window.turnstile.render(node, {
        sitekey: turnstileSiteKey,
        action: turnstileAction,
        theme,
        callback: (token) => {
          turnstileRef.current = token;
        },
        'expired-callback': () => {
          turnstileRef.current = '';
        },
        'error-callback': () => {
          setWidgetBlocked(true);
        },
      });
      widgetIdRef.current = widgetId;
    });
    return () => {
      cancel = true;
      if (widgetId && window.turnstile) window.turnstile.remove(widgetId);
      if (widgetIdRef.current === widgetId) widgetIdRef.current = undefined;
    };
  }, [near, theme]);

  function resetTurnstile() {
    turnstileRef.current = '';
    const id = widgetIdRef.current;
    if (id && window.turnstile) window.turnstile.reset(id);
  }

  async function freshFormToken(current: string | null): Promise<string | null> {
    if (current && !formTokenNeedsRefresh(current, now())) return current;
    if (current && formTokenNeedsRefresh(current, now())) {
      if (ageRefreshRef.current) return current;
      ageRefreshRef.current = true;
    }
    const next = await requestFormToken();
    if (!next) {
      ageRefreshRef.current = false;
      return null;
    }
    setFormToken(next);
    return next;
  }

  async function waitForTurnstileToken(): Promise<string> {
    let waited = 0;
    while (!turnstileRef.current && waited < 4_000) {
      await sleep(50);
      waited += 50;
    }
    return turnstileRef.current;
  }

  async function post(token: string, turnstileToken: string): Promise<ContactOutcome> {
    const response = await fetch(contactEndpoint('/api/contact'), {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        name: name.trim(),
        email: email.trim(),
        message: message.trim(),
        website,
        formToken: token,
        turnstileToken,
      }),
    });
    let body: unknown = null;
    try {
      body = await response.json();
    } catch {
      body = null;
    }
    return classifyContactResponse(
      response.status,
      body,
      response.headers.get('Retry-After'),
    );
  }

  async function applyOutcome(
    outcome: ContactOutcome,
    token: string,
    isRetry: boolean,
  ) {
    if (outcome.kind === 'success') {
      setName('');
      setEmail('');
      setMessage('');
      setWebsite('');
      setErrors({});
      resetTurnstile();
      setStatus({ tone: 'polite', text: 'Thank you. Your message is on its way.' });
      return;
    }

    if (outcome.kind === 'fields') {
      setErrors(outcome.fields);
      focusFirst(outcome.fields);
      resetTurnstile();
      setStatus({ tone: 'alert', text: 'Check the fields above and try again.' });
      return;
    }

    if (outcome.kind === 'form_token' || outcome.kind === 'turnstile_timeout') {
      resetTurnstile();
      const next = await requestFormToken();
      if (next) setFormToken(next);
      setStatus({ tone: 'alert', text: refreshed });
      return;
    }

    if (outcome.kind === 'turnstile') {
      resetTurnstile();
      setStatus({ tone: 'alert', text: outcome.message });
      return;
    }

    if (outcome.kind === 'rate_limited') {
      setStatus({
        tone: 'alert',
        text: `Too many messages from this network. Please wait ${outcome.retryAfterSeconds} seconds, then try again.`,
      });
      return;
    }

    if (outcome.kind === 'unavailable') {
      if (isRetry) {
        setStatus({ tone: 'alert', emailFallback: 'send' });
        return;
      }
      setStatus({ tone: 'polite', text: waking });
      resetTurnstile();
      const nextToken = (await requestFormToken()) ?? token;
      if (nextToken !== token) setFormToken(nextToken);
      await sleep(outcome.waitMs);
      const nextTurnstile = await waitForTurnstileToken();
      if (!nextTurnstile) {
        setStatus({ tone: 'alert', emailFallback: 'send' });
        return;
      }
      const second = await post(nextToken, nextTurnstile);
      await applyOutcome(second, nextToken, true);
      return;
    }

    resetTurnstile();
    setStatus({ tone: 'alert', text: generic });
  }

  async function run() {
    setErrors({});
    const clientErrors = validateContact({ name, email, message });
    if (Object.keys(clientErrors).length > 0) {
      setErrors(clientErrors);
      focusFirst(clientErrors);
      return;
    }
    if (widgetBlocked) {
      setStatus({ tone: 'alert', text: blocked });
      return;
    }

    const token = await freshFormToken(formToken);
    if (!token) {
      setStatus({ tone: 'alert', emailFallback: 'ready' });
      return;
    }

    const turnstileToken = turnstileRef.current;
    if (!turnstileToken) {
      setStatus({ tone: 'alert', text: needCheck });
      return;
    }

    try {
      const outcome = await post(token, turnstileToken);
      await applyOutcome(outcome, token, false);
    } catch {
      setStatus({ tone: 'alert', text: generic });
    }
  }

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (sendingRef.current) return;
    sendingRef.current = true;
    setSending(true);
    void run().finally(() => {
      sendingRef.current = false;
      setSending(false);
    });
  }

  const messageDescribedBy = [
    errors.message ? 'contact-message-error' : '',
    'contact-message-count',
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <div ref={rootRef} className="mt-8">
      <form className="grid gap-4" onSubmit={onSubmit} noValidate>
        <Field
          id="contact-name"
          label="Name"
          autoComplete="name"
          maxLength={NAME_MAX}
          value={name}
          error={errors.name}
          onChange={(value) => {
            setName(value);
            setErrors((current) => ({ ...current, name: undefined }));
          }}
        />
        <Field
          id="contact-email"
          label="Email"
          type="email"
          autoComplete="email"
          inputMode="email"
          maxLength={EMAIL_MAX}
          value={email}
          error={errors.email}
          onChange={(value) => {
            setEmail(value);
            setErrors((current) => ({ ...current, email: undefined }));
          }}
        />
        <div className="grid gap-1.5">
          <label htmlFor="contact-message" className="text-ink text-sm font-semibold">
            Message
          </label>
          <textarea
            id="contact-message"
            name="message"
            autoComplete="off"
            maxLength={MESSAGE_MAX}
            rows={6}
            value={message}
            aria-invalid={Boolean(errors.message)}
            aria-describedby={messageDescribedBy}
            onChange={(event) => {
              setMessage(event.target.value);
              setErrors((current) => ({ ...current, message: undefined }));
            }}
            className={inputClass(Boolean(errors.message))}
          />
          {errors.message ? (
            <p id="contact-message-error" className="text-progress-fg text-sm">
              {errors.message}
            </p>
          ) : null}
          <p id="contact-message-count" className="text-muted text-sm">
            {message.length} / {MESSAGE_MAX}
          </p>
        </div>

        <div className="contact-honeypot" aria-hidden="true">
          <label htmlFor="contact-website">Website</label>
          <input
            id="contact-website"
            name="website"
            type="text"
            tabIndex={-1}
            autoComplete="off"
            value={website}
            onChange={(event) => setWebsite(event.target.value)}
          />
        </div>

        <div ref={widgetRef} className="min-h-16" />
        {widgetBlocked ? <p className="text-progress-fg text-sm">{blocked}</p> : null}

        <div aria-live="polite" aria-atomic="true">
          {status?.tone === 'polite' ? <p>{status.text}</p> : null}
        </div>
        {status?.tone === 'alert' ? (
          <p role="alert">
            {'emailFallback' in status ? (
              <>
                {status.emailFallback === 'ready'
                  ? 'The form is not ready yet. Try again in a moment, or '
                  : 'The form could not send just now. Try again in a moment, or '}
                <a
                  className="text-accent font-semibold underline"
                  href={`mailto:${profile.email}`}
                >
                  email me
                </a>
                .
              </>
            ) : (
              status.text
            )}
          </p>
        ) : null}

        <div>
          <button type="submit" className={buttonStyles.primary} disabled={sending}>
            {sending ? 'Sending…' : 'Send message'}
          </button>
        </div>
      </form>
      <p className="text-muted mt-4 text-sm">
        I store your message only so I can reply. Your IP address is kept only as a
        one-way hash, to limit spam. Cloudflare Turnstile checks for bots.{' '}
        <a
          className="text-accent font-semibold underline"
          href="https://www.cloudflare.com/privacypolicy/"
        >
          Cloudflare&apos;s privacy policy
        </a>
        .
      </p>
    </div>
  );
}

function Field({
  id,
  label,
  value,
  error,
  onChange,
  type = 'text',
  autoComplete,
  maxLength,
  inputMode,
}: {
  id: string;
  label: string;
  value: string;
  error?: string;
  onChange: (value: string) => void;
  type?: string;
  autoComplete: string;
  maxLength: number;
  inputMode?: 'email';
}) {
  const errorId = `${id}-error`;
  return (
    <div className="grid gap-1.5">
      <label htmlFor={id} className="text-ink text-sm font-semibold">
        {label}
      </label>
      <input
        id={id}
        name={id.replace('contact-', '')}
        type={type}
        autoComplete={autoComplete}
        inputMode={inputMode}
        maxLength={maxLength}
        value={value}
        aria-invalid={Boolean(error)}
        aria-describedby={error ? errorId : undefined}
        onChange={(event) => onChange(event.target.value)}
        className={inputClass(Boolean(error))}
      />
      {error ? (
        <p id={errorId} className="text-progress-fg text-sm">
          {error}
        </p>
      ) : null}
    </div>
  );
}

function inputClass(invalid: boolean): string {
  return [
    'w-full rounded-xl border bg-surface px-3 py-2 text-base text-ink',
    invalid ? 'border-progress-fg' : 'border-line',
  ].join(' ');
}

function focusFirst(errors: ContactFieldErrors) {
  for (const field of fieldOrder) {
    if (!errors[field]) continue;
    document.getElementById(`contact-${field}`)?.focus();
    return;
  }
}

async function requestFormToken(): Promise<string | null> {
  try {
    const response = await fetch(contactEndpoint('/api/contact/token'));
    if (!response.ok) return null;
    const body: unknown = await response.json();
    if (
      typeof body === 'object' &&
      body !== null &&
      'formToken' in body &&
      typeof body.formToken === 'string' &&
      body.formToken.length > 0
    ) {
      return body.formToken;
    }
    return null;
  } catch {
    return null;
  }
}
