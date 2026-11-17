import { HOME_URL, loginUrl, safeReturnUrl } from './return-url';

describe('safeReturnUrl', () => {
  it.each(['/accounts', '/transactions?kind=expense', '/settings#password'])(
    'keeps a path of the app: %s',
    (url) => {
      expect(safeReturnUrl(url)).toBe(url);
    },
  );

  it.each([
    ['nothing', undefined],
    ['an empty value', ''],
    ['an absolute URL', 'https://evil.example/phish'],
    ['a protocol-relative URL', '//evil.example'],
    ['a backslash trick', '/\\evil.example'],
    ['a relative path', 'accounts'],
    ['the login page itself', '/login?returnUrl=%2Faccounts'],
  ])('goes home instead of %s', (_, url) => {
    expect(safeReturnUrl(url)).toBe(HOME_URL);
  });
});

describe('loginUrl', () => {
  it('asks the login page to come back to the current page', () => {
    expect(loginUrl('/transactions?kind=expense')).toBe(
      '/login?returnUrl=%2Ftransactions%3Fkind%3Dexpense',
    );
  });

  it('needs no return address for the home page', () => {
    expect(loginUrl(HOME_URL)).toBe('/login');
  });
});
