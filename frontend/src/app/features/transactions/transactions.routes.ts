import { Routes } from '@angular/router';
import { NewExpensePage } from './pages/new-expense-page';
import { TransactionsPage } from './pages/transactions-page';

export const TRANSACTIONS_ROUTES: Routes = [
  { path: '', component: TransactionsPage, title: $localize`:Page title:Movimenti` },
  { path: 'new', component: NewExpensePage, title: $localize`:Page title:Nuova spesa` },
];
