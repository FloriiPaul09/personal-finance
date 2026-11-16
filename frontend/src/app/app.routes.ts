import { Routes } from '@angular/router';
import { Shell } from './core';

// Every feature is loaded on demand from its barrel (features/<feature>/index.ts).
// Paths are English and stay the same in every language (docs/frontend/routing.md).
export const routes: Routes = [
  {
    path: '',
    component: Shell,
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
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
