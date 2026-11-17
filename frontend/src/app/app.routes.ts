import { Routes } from '@angular/router';
import { authGuard, guestGuard, Shell, UnavailablePage } from './core';
import { DEV_ROUTES } from './dev-routes';

// Every feature is loaded on demand from its barrel (features/<feature>/index.ts).
// Paths are English and stay the same in every language (docs/frontend/routing.md).
export const routes: Routes = [
  {
    path: 'login',
    canMatch: [guestGuard],
    loadChildren: () => import('./features/auth').then((m) => m.AUTH_ROUTES),
  },
  {
    path: 'unavailable',
    component: UnavailablePage,
    title: $localize`:Page title:Server non raggiungibile`,
  },
  {
    // Every page of the shell needs a session: without one, the login page opens.
    path: '',
    component: Shell,
    canMatch: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadChildren: () => import('./features/dashboard').then((m) => m.DASHBOARD_ROUTES),
      },
      {
        path: 'transactions',
        loadChildren: () => import('./features/transactions').then((m) => m.TRANSACTIONS_ROUTES),
      },
      {
        // Not in the navigation: reached from the movement forms and the movement list.
        path: 'transfers',
        loadChildren: () => import('./features/transfers').then((m) => m.TRANSFERS_ROUTES),
      },
      {
        path: 'budgets',
        loadChildren: () => import('./features/budgets').then((m) => m.BUDGETS_ROUTES),
      },
      {
        path: 'accounts',
        loadChildren: () => import('./features/accounts').then((m) => m.ACCOUNTS_ROUTES),
      },
      {
        path: 'categories',
        loadChildren: () => import('./features/categories').then((m) => m.CATEGORIES_ROUTES),
      },
      {
        path: 'settings',
        loadChildren: () => import('./features/settings').then((m) => m.SETTINGS_ROUTES),
      },
      // Catalog of the shared components, only in development (docs/frontend/testing.md).
      ...DEV_ROUTES,
      { path: '**', redirectTo: 'dashboard' },
    ],
  },
];
