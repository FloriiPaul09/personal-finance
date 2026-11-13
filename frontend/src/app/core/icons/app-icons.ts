import {
  EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

/** Where the build copies the Material Symbols SVG files (angular.json, `assets`). */
export const ICONS_PATH = 'icons';

/** Icon names of Material Symbols: lower-case words joined by `_`, optionally `-fill`. */
const ICON_NAME = /^[a-z0-9]+(?:_[a-z0-9]+)*(?:-fill)?$/;

/**
 * Resolves `<mat-icon svgIcon="name">` to the self-hosted SVG of Material Symbols.
 * Only the icons actually shown are downloaded, instead of a 4 MB icon font.
 * Names that do not look like an icon name, or that use a namespace, are not resolved.
 */
export function resolveIconUrl(
  sanitizer: DomSanitizer,
  name: string,
  namespace: string,
): SafeResourceUrl | null {
  if (namespace !== '' || !ICON_NAME.test(name)) {
    return null;
  }
  // Safe: the URL is built from a validated name and points to our own assets.
  return sanitizer.bypassSecurityTrustResourceUrl(`${ICONS_PATH}/${name}.svg`);
}

export function provideAppIcons(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(() => {
      const sanitizer = inject(DomSanitizer);
      inject(MatIconRegistry).addSvgIconResolver((name, namespace) =>
        resolveIconUrl(sanitizer, name, namespace),
      );
    }),
  ]);
}
