import { CategoryKind } from '../../../core';

export interface CategoryKindInfo {
  readonly kind: CategoryKind;
  /** Name of one category of this kind ("Uscita"). */
  readonly label: string;
  /** Name of the tab with every category of this kind ("Uscite"). */
  readonly tab: string;
}

/** Expenses first: the API lists them first, and they are most of the categories. */
export const CATEGORY_KINDS: readonly CategoryKindInfo[] = [
  {
    kind: 'expense',
    label: $localize`:Kind of category:Uscita`,
    tab: $localize`:Tab of the categories page:Uscite`,
  },
  {
    kind: 'income',
    label: $localize`:Kind of category:Entrata`,
    tab: $localize`:Tab of the categories page:Entrate`,
  },
];

/** The kind in the query string (`?kind=income`); anything else is the expenses. */
export function categoryKindOf(value: string | null | undefined): CategoryKind {
  return value === 'income' ? 'income' : 'expense';
}
