import { Routes } from '@angular/router';
import { FORM_PAGE } from '../../core';

import { AccountFormPage } from './pages/account-form-page';
import { AccountsPage } from './pages/accounts-page';

export const ACCOUNTS_ROUTES: Routes = [
  { path: '', component: AccountsPage, title: $localize`:Page title:Conti` },
  {
    path: 'new',
    component: AccountFormPage,
    title: $localize`:Page title:Nuovo conto`,
    data: FORM_PAGE,
  },
  {
    path: ':id',
    component: AccountFormPage,
    title: $localize`:Page title:Modifica conto`,
    data: FORM_PAGE,
  },
];
