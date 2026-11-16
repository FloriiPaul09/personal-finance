import { ifMatch } from './versions';

describe('ifMatch', () => {
  it('sends the version as a strong entity tag', () => {
    expect(ifMatch(81723).get('If-Match')).toBe('"81723"');
  });

  it('accepts version 0', () => {
    expect(ifMatch(0).get('If-Match')).toBe('"0"');
  });

  it.each([-1, 1.5, Number.NaN, Number.MAX_SAFE_INTEGER + 2])('refuses %s', (version) => {
    expect(() => ifMatch(version)).toThrow(RangeError);
  });
});
