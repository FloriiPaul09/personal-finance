import { Routes } from '@angular/router';
import { TransferFormPage } from './pages/transfer-form-page';

// No list: transfers appear in the movement list as two legs (docs/backend/api-endpoints.md#transfers).
export const TRANSFERS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: '/transactions?kind=transfer' },
  { path: 'new', component: TransferFormPage, title: $localize`:Page title:Nuovo trasferimento` },
  {
    path: ':id',
    component: TransferFormPage,
    title: $localize`:Page title:Modifica trasferimento`,
  },
];
