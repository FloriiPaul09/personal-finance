import { Routes } from '@angular/router';

/**
 * Pages for developers only (catalog of the shared components). The production build replaces
 * this file with dev-routes.prod.ts (angular.json `fileReplacements`), so none of their code, not
 * even what they share with the app, reaches the production bundles.
 */
export const DEV_ROUTES: Routes = [
  {
    path: 'dev',
    loadChildren: () => import('./features/dev').then((m) => m.DEV_ROUTES),
  },
];
