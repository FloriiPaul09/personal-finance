/**
 * Runs date tests in time zones far from UTC (docs/frontend/testing.md#unit-tests-vitest).
 * Node reads `process.env.TZ` again whenever it changes, so `Date` follows it within the run.
 */
declare const process: { env: Record<string, string | undefined> };

/** UTC+14, UTC-8/-7 with daylight saving, and the app's own zone (UTC+1/+2). */
export const TIME_ZONES = ['Pacific/Kiritimati', 'America/Los_Angeles', 'Europe/Rome'] as const;

/** Switches the time zone; returns the function that restores the previous one. */
export function setTimeZone(timeZone: string): () => void {
  const previous = process.env['TZ'];
  process.env['TZ'] = timeZone;
  return () => {
    if (previous === undefined) {
      delete process.env['TZ'];
    } else {
      process.env['TZ'] = previous;
    }
  };
}

/** Every test of the enclosing `describe` runs in `timeZone`. */
export function useTimeZone(timeZone: string): void {
  let restore: () => void = () => undefined;
  beforeEach(() => {
    restore = setTimeZone(timeZone);
  });
  afterEach(() => restore());
}
