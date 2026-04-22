import '@testing-library/jest-dom';
import { vi, beforeEach, afterEach, beforeAll, afterAll } from 'vitest';
import { server } from './mocks/server';

// Silence known MUI false-positives in jsdom that are not indicative of real bugs:
//  - Select "out-of-range value": console.warn from MUI Select when options load async
//  - Popper "anchorEl invalid": Autocomplete/Popper cannot measure layout in jsdom
const SUPPRESSED_WARNINGS = [
  'out-of-range value',
  'The `anchorEl` prop provided to the component is invalid',
];

function makeFilteredConsole(
  _original: (...args: unknown[]) => void,
  stderrLabel: string
) {
  return (...args: unknown[]) => {
    const msg = typeof args[0] === 'string' ? args[0] : '';
    if (SUPPRESSED_WARNINGS.some((w) => msg.includes(w))) return;
    process.stderr.write(`[${stderrLabel}] ` + args.map(String).join(' ') + '\n');
  };
}

beforeEach(() => {
  vi.spyOn(console, 'error').mockImplementation(
    makeFilteredConsole(console.error, 'error')
  );
  vi.spyOn(console, 'warn').mockImplementation(
    makeFilteredConsole(console.warn, 'warn')
  );
});

afterEach(() => {
  vi.mocked(console.error).mockRestore();
  vi.mocked(console.warn).mockRestore();
});

beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
