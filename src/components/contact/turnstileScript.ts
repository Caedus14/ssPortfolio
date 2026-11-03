const SCRIPT_SRC =
  'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';

let loading: Promise<'ready' | 'blocked'> | null = null;

/** Loads the explicit Turnstile API once. A script error is a blocked check, not a throw. */
export function ensureTurnstileScript(): Promise<'ready' | 'blocked'> {
  if (window.turnstile) return Promise.resolve('ready');
  if (loading) return loading;

  loading = new Promise((resolve) => {
    const script = document.createElement('script');
    script.src = SCRIPT_SRC;
    script.async = true;
    script.dataset.turnstile = 'true';
    script.onload = () => resolve(window.turnstile ? 'ready' : 'blocked');
    script.onerror = () => resolve('blocked');
    document.head.appendChild(script);
  });

  return loading;
}
