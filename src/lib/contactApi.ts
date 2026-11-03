import { contactApiUrl } from '../config/site.ts';
import type { ContactField, ContactFieldErrors } from './contactValidation.ts';

export const FORM_TOKEN_REFRESH_AFTER_MS = 110 * 60 * 1000;
export const WAKE_RETRY_CAP_MS = 15_000;
export const WAKE_RETRY_DEFAULT_MS = 1_500;

const fields = new Set<ContactField>(['name', 'email', 'message']);

export type ContactOutcome =
  | { kind: 'success' }
  | { kind: 'fields'; fields: ContactFieldErrors }
  | { kind: 'form_token'; code: string }
  | { kind: 'turnstile_timeout' }
  | { kind: 'turnstile'; message: string }
  | { kind: 'rate_limited'; retryAfterSeconds: number }
  | { kind: 'unavailable'; waitMs: number }
  | { kind: 'generic' };

export function contactEndpoint(path: '/api/contact' | '/api/contact/token'): string {
  const base = contactApiUrl.replace(/\/$/, '');
  return `${base}${path}`;
}

/** The token payload is base64url(utf8 milliseconds). Returns null when it is not that shape. */
export function formTokenIssuedAt(token: string): number | null {
  const payload = token.split('.')[0];
  if (!payload) return null;
  try {
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    const pad = base64.length % 4 === 0 ? '' : '='.repeat(4 - (base64.length % 4));
    const text = atob(base64 + pad);
    if (!/^\d+$/.test(text)) return null;
    const ms = Number(text);
    return Number.isSafeInteger(ms) ? ms : null;
  } catch {
    return null;
  }
}

export function formTokenNeedsRefresh(token: string, nowMs: number): boolean {
  const issued = formTokenIssuedAt(token);
  if (issued === null) return false;
  return nowMs - issued >= FORM_TOKEN_REFRESH_AFTER_MS;
}

export function wakeWaitMs(
  retryAfterSeconds: unknown,
  retryAfterHeader: string | null,
): number {
  const fromBody =
    typeof retryAfterSeconds === 'number' && retryAfterSeconds > 0
      ? retryAfterSeconds * 1000
      : undefined;
  const headerSeconds =
    retryAfterHeader && /^\d+$/.test(retryAfterHeader.trim())
      ? Number(retryAfterHeader.trim())
      : undefined;
  const fromHeader =
    headerSeconds !== undefined && headerSeconds > 0 ? headerSeconds * 1000 : undefined;
  return Math.min(fromBody ?? fromHeader ?? WAKE_RETRY_DEFAULT_MS, WAKE_RETRY_CAP_MS);
}

export function classifyContactResponse(
  status: number,
  body: unknown,
  retryAfterHeader: string | null,
): ContactOutcome {
  const record = isRecord(body) ? body : {};
  const error = typeof record.error === 'string' ? record.error : '';
  const fieldErrors = readFields(record.fields);

  if (status === 200 && record.status === 'accepted') return { kind: 'success' };

  if (status === 400 && error.startsWith('form_token_')) {
    return { kind: 'form_token', code: error };
  }

  if (status === 400 && error === 'turnstile_timeout') {
    return { kind: 'turnstile_timeout' };
  }

  if (
    status === 400 &&
    (error === 'turnstile_required' || error === 'turnstile_failed')
  ) {
    return {
      kind: 'turnstile',
      message:
        typeof record.fields === 'object' &&
        record.fields !== null &&
        'turnstileToken' in record.fields &&
        typeof (record.fields as { turnstileToken?: unknown }).turnstileToken ===
          'string'
          ? (record.fields as { turnstileToken: string }).turnstileToken
          : 'Complete the check again and try again.',
    };
  }

  if (status === 400 && Object.keys(fieldErrors).length > 0) {
    return { kind: 'fields', fields: fieldErrors };
  }

  if (status === 429) {
    const seconds =
      positiveSeconds(record.retryAfterSeconds) ?? positiveSeconds(retryAfterHeader);
    return { kind: 'rate_limited', retryAfterSeconds: seconds ?? 60 };
  }

  if (status === 503) {
    return {
      kind: 'unavailable',
      waitMs: wakeWaitMs(record.retryAfterSeconds, retryAfterHeader),
    };
  }

  return { kind: 'generic' };
}

function readFields(value: unknown): ContactFieldErrors {
  if (!isRecord(value)) return {};
  const errors: ContactFieldErrors = {};
  for (const key of fields) {
    const message = value[key];
    if (typeof message === 'string' && message.length > 0) errors[key] = message;
  }
  return errors;
}

function positiveSeconds(value: unknown): number | undefined {
  if (typeof value === 'number' && Number.isFinite(value) && value > 0)
    return Math.ceil(value);
  if (typeof value === 'string' && /^\d+$/.test(value.trim())) {
    const parsed = Number(value.trim());
    return parsed > 0 ? parsed : undefined;
  }
  return undefined;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}
