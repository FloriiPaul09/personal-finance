import { Routes } from '@angular/router';
import { FORM_PAGE } from '../../core';

import { TransactionFormPage } from './pages/transaction-form-page';
import { TransactionsPage } from './pages/transactions-page';

export const TRANSACTIONS_ROUTES: Routes = [
  { path: '', component: TransactionsPage, title: $localize`:Page title:Movimenti` },
  {
    path: 'new',
    component: TransactionFormPage,
    title: $localize`:Page title:Nuovo movimento`,
    data: FORM_PAGE,
  },
  {
    path: ':id',
    component: TransactionFormPage,
    title: $localize`:Page title:Modifica movimento`,
    data: FORM_PAGE,
  },
];
