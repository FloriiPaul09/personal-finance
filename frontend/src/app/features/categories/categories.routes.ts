import { Routes } from '@angular/router';
import { CategoriesPage } from './pages/categories-page';
import { CategoryFormPage } from './pages/category-form-page';

export const CATEGORIES_ROUTES: Routes = [
  { path: '', component: CategoriesPage, title: $localize`:Page title:Categorie` },
  { path: 'new', component: CategoryFormPage, title: $localize`:Page title:Nuova categoria` },
  { path: ':id', component: CategoryFormPage, title: $localize`:Page title:Modifica categoria` },
];
