export const NAME_MAX = 100;
export const EMAIL_MAX = 254;
export const MESSAGE_MIN = 10;
export const MESSAGE_MAX = 2000;
export const MAX_LINKS = 2;

export const contactMessages = {
  nameLength: 'Name must be 1 to 100 characters.',
  nameControl: 'Name cannot include control characters.',
  email: 'Enter an email address, up to 254 characters, without spaces.',
  messageLength: 'Message must be 10 to 2000 characters.',
  messageControl: 'Message cannot include control characters.',
  messageHtml: 'Message cannot include HTML.',
  messageLinks: 'Message can include at most 2 links.',
} as const;

export type ContactField = 'name' | 'email' | 'message';
export type ContactFieldErrors = Partial<Record<ContactField, string>>;

export function validateContact(input: {
  name: string;
  email: string;
  message: string;
}): ContactFieldErrors {
  const errors: ContactFieldErrors = {};
  const name = input.name.trim();
  const email = input.email.trim();
  const message = input.message.trim();

  if (name.length < 1 || name.length > NAME_MAX) {
    errors.name = contactMessages.nameLength;
  } else if (hasForbiddenControl(name, false)) {
    errors.name = contactMessages.nameControl;
  }

  if (!isPlausibleEmail(email)) {
    errors.email = contactMessages.email;
  }

  if (message.length < MESSAGE_MIN || message.length > MESSAGE_MAX) {
    errors.message = contactMessages.messageLength;
  } else if (hasForbiddenControl(message, true)) {
    errors.message = contactMessages.messageControl;
  } else if (containsHtmlTag(message)) {
    errors.message = contactMessages.messageHtml;
  } else if (countLinks(message) > MAX_LINKS) {
    errors.message = contactMessages.messageLinks;
  }

  return errors;
}

export function isPlausibleEmail(email: string): boolean {
  if (email.length < 3 || email.length > EMAIL_MAX) return false;
  if ([...email].some((char) => /\s/u.test(char) || isControl(char))) return false;
  const at = email.indexOf('@');
  if (at <= 0 || at !== email.lastIndexOf('@')) return false;
  const domain = email.slice(at + 1);
  const dot = domain.indexOf('.');
  if (dot <= 0 || domain.endsWith('.')) return false;
  if (domain.includes('..')) return false;
  return true;
}

function hasForbiddenControl(value: string, allowNewlines: boolean): boolean {
  for (const char of value) {
    if (!isControl(char)) continue;
    if (allowNewlines && (char === '\n' || char === '\r' || char === '\t')) continue;
    return true;
  }
  return false;
}

function isControl(char: string): boolean {
  const code = char.charCodeAt(0);
  return code <= 31 || (code >= 127 && code <= 159);
}

export function containsHtmlTag(value: string): boolean {
  for (let i = 0; i < value.length - 1; i += 1) {
    if (value[i] !== '<') continue;
    const next = value[i + 1] ?? '';
    if (isLetter(next) || next === '!' || next === '?') return true;
    if (next === '/' && i + 2 < value.length && isLetter(value[i + 2] ?? ''))
      return true;
  }
  return false;
}

export function countLinks(value: string): number {
  let count = 0;
  let i = 0;
  while (i < value.length) {
    if (
      startsWith(value, i, 'https://') ||
      startsWith(value, i, 'http://') ||
      startsWith(value, i, 'www.')
    ) {
      count += 1;
      while (i < value.length && !/\s/u.test(value[i] ?? '')) i += 1;
      continue;
    }
    i += 1;
  }
  return count;
}

function isLetter(char: string): boolean {
  return /\p{L}/u.test(char);
}

function startsWith(value: string, index: number, marker: string): boolean {
  return value.slice(index, index + marker.length).toLowerCase() === marker;
}
