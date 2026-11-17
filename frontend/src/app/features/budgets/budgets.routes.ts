import { Routes } from '@angular/router';
import { FORM_PAGE } from '../../core';

import { BudgetFormPage } from './pages/budget-form-page';
import { BudgetsPage } from './pages/budgets-page';

export const BUDGETS_ROUTES: Routes = [
  { path: '', component: BudgetsPage, title: $localize`:Page title:Budget` },
  {
    path: 'new',
    component: BudgetFormPage,
    title: $localize`:Page title:Nuovo budget`,
    data: FORM_PAGE,
  },
  {
    path: ':categoryId',
    component: BudgetFormPage,
    title: $localize`:Page title:Modifica budget`,
    data: FORM_PAGE,
  },
];
