/// <reference types="node" />
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Every icon the app names exists among the self-hosted SVGs (`angular.json` copies
 * `@material-symbols/svg-400/outlined` to `icons/`). Unit tests use a fake icon registry, so a
 * wrong name (Material Symbols has `keyboard_arrow_down`, not the font's `expand_more`) would show
 * up only as a 404 and an error message in the browser.
 *
 * Names are read from the sources: `svgIcon="…"` and `icon="…"` attributes, quoted names inside
 * `[svgIcon]="…"` / `[icon]="…"` bindings, and `icon: '…'` properties (navigation, account
 * types). Navigation icons are also shown filled when their page is open (`-fill`).
 */
const SOURCES = 'src/app';
const SYMBOLS = 'node_modules/@material-symbols/svg-400/outlined';
const FILLED = 'src/app/core/layout/nav-items.ts';

function sourceFiles(folder: string): string[] {
  return readdirSync(folder, { withFileTypes: true }).flatMap((entry) => {
    const path = join(folder, entry.name);
    if (entry.isDirectory()) {
      return entry.name === 'testing' ? [] : sourceFiles(path);
    }
    return /\.(ts|html)$/.test(entry.name) && !entry.name.endsWith('.spec.ts') ? [path] : [];
  });
}

/** The icon names a source file uses, comments left out. */
export function iconNames(source: string): string[] {
  const code = source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, '');
  const names = [
    ...[...code.matchAll(/\b(?:svgIcon|icon)="([a-z0-9_]+(?:-fill)?)"/g)].map((m) => m[1]),
    ...[...code.matchAll(/\[(?:svgIcon|icon)\]="([^"]*)"/g)].flatMap((m) =>
      [...m[1].matchAll(/'([a-z0-9_]+(?:-fill)?)'/g)].map((n) => n[1]),
    ),
    ...[...code.matchAll(/\bicon: '([a-z0-9_]+)'/g)].map((m) => m[1]),
  ];
  return [...new Set(names)];
}

describe('icons in use', () => {
  it('finds names in attributes, bindings and properties, not in comments', () => {
    expect(
      iconNames(`
        /** <mat-icon svgIcon="name"> */
        // icon: 'commented'
        <mat-icon svgIcon="add" /> <app-empty-state icon="savings" />
        <mat-icon [svgIcon]="open() ? 'keyboard_arrow_up' : 'keyboard_arrow_down'" />
        { icon: 'home' }
      `),
    ).toEqual(['add', 'savings', 'keyboard_arrow_up', 'keyboard_arrow_down', 'home']);
  });

  it('are all among the self-hosted Material Symbols', () => {
    const missing: string[] = [];
    for (const file of sourceFiles(SOURCES)) {
      const names = iconNames(readFileSync(file, 'utf8'));
      const wanted =
        file.replaceAll('\\', '/') === FILLED ? names.flatMap((n) => [n, `${n}-fill`]) : names;
      for (const name of wanted) {
        if (!existsSync(join(SYMBOLS, `${name}.svg`))) {
          missing.push(`${name} (${file})`);
        }
      }
    }

    expect(missing).toEqual([]);
  });

  it('reads the navigation and some pages', () => {
    const files = sourceFiles(SOURCES).map((file) => file.replaceAll('\\', '/'));
    expect(files).toContain(FILLED);
    expect(iconNames(readFileSync(FILLED, 'utf8')).length).toBeGreaterThanOrEqual(5);
  });
});
