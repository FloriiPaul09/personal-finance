import { newId } from './new-id';

const UUID_V7 = /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

describe('newId', () => {
  it('makes a UUID version 7 with the RFC 9562 variant, as the API requires', () => {
    expect(newId()).toMatch(UUID_V7);
  });

  it('starts with the current time in milliseconds', () => {
    const before = Date.now();
    const id = newId();
    const after = Date.now();

    const millis = parseInt(id.replace(/-/g, '').slice(0, 12), 16);
    expect(millis).toBeGreaterThanOrEqual(before);
    expect(millis).toBeLessThanOrEqual(after);
  });

  it('makes unique ids that sort in creation order, also within the same millisecond', () => {
    const ids = Array.from({ length: 2000 }, () => newId());

    expect(new Set(ids).size).toBe(ids.length);
    expect([...ids].sort()).toEqual(ids);
  });
});
