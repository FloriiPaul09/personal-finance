import { Routes } from '@angular/router';
import { SettingsPage } from './pages/settings-page';

export const SETTINGS_ROUTES: Routes = [
  { path: '', component: SettingsPage, title: $localize`:Page title:Impostazioni` },
];
