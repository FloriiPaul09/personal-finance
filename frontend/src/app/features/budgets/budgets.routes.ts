import { Routes } from '@angular/router';
import { BudgetsPage } from './pages/budgets-page';

export const BUDGETS_ROUTES: Routes = [
  { path: '', component: BudgetsPage, title: $localize`:Page title:Budget` },
];
