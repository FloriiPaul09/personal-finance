import { Routes } from '@angular/router';
import { TransactionFormPage } from './pages/transaction-form-page';
import { TransactionsPage } from './pages/transactions-page';

export const TRANSACTIONS_ROUTES: Routes = [
  { path: '', component: TransactionsPage, title: $localize`:Page title:Movimenti` },
  { path: 'new', component: TransactionFormPage, title: $localize`:Page title:Nuovo movimento` },
  { path: ':id', component: TransactionFormPage, title: $localize`:Page title:Modifica movimento` },
];
