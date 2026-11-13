import { provideHttpClient } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideAppIcons, providePageTitles } from './core';
import { routes } from './app.routes';

// The locale (it) is not provided here: it comes from `i18n.sourceLocale` in angular.json,
// which sets LOCALE_ID, the locale data and <html lang> (docs/frontend/app-structure.md#i18n).
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(),
    providePageTitles(),
    provideAppIcons(),
  ],
};
