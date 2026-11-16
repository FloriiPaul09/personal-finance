import { SecurityContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DomSanitizer } from '@angular/platform-browser';
import { resolveIconUrl } from './app-icons';

describe('resolveIconUrl', () => {
  let sanitizer: DomSanitizer;

  beforeEach(() => {
    sanitizer = TestBed.inject(DomSanitizer);
  });

  function urlOf(name: string, namespace = ''): string | null {
    const url = resolveIconUrl(sanitizer, name, namespace);
    return url === null ? null : sanitizer.sanitize(SecurityContext.RESOURCE_URL, url);
  }

  it('points an icon name to its self-hosted SVG', () => {
    expect(urlOf('receipt_long')).toBe('icons/receipt_long.svg');
  });

  it('resolves the filled variant', () => {
    expect(urlOf('home-fill')).toBe('icons/home-fill.svg');
  });

  it.each(['../secret', 'https://example.com/x', 'Home', 'home.svg', '', 'home-fill-fill'])(
    'refuses a name that is not an icon name: %s',
    (name) => {
      expect(urlOf(name)).toBeNull();
    },
  );

  it('ignores namespaced icons', () => {
    expect(urlOf('home', 'brand')).toBeNull();
  });
});
