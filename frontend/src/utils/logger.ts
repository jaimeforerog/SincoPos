const isDev = import.meta.env.DEV;

export const logger = {
  warn: (...args: unknown[]): void => { if (isDev) console.warn(...args); },
  error: (...args: unknown[]): void => { if (isDev) console.error(...args); },
};
