import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import {
  provideApiHttp,
  provideAppErrorHandler,
  provideAppIcons,
  providePageTitles,
  provideSnackBars,
} from './core';
import { routes } from './app.routes';

// The locale (it) is not provided here: it comes from `i18n.sourceLocale` in angular.json,
// which sets LOCALE_ID, the locale data and <html lang> (docs/frontend/app-structure.md#i18n).
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideAppErrorHandler(),
    provideRouter(routes, withComponentInputBinding()),
    provideApiHttp(),
    providePageTitles(),
    provideAppIcons(),
    provideSnackBars(),
  ],
};
