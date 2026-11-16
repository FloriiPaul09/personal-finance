/** A destination of the main navigation (bottom bar on phones, rail on wider screens). */
export interface NavItem {
  /** Absolute route path. */
  readonly path: string;
  /** Material Symbols name; the `-fill` variant is shown when the item is active. */
  readonly icon: string;
  readonly label: string;
}

/** At most five items: the M3 navigation bar does not fit more on a phone. */
export const NAV_ITEMS: readonly NavItem[] = [
  { path: '/dashboard', icon: 'home', label: $localize`:Main navigation item:Home` },
  {
    path: '/transactions',
    icon: 'receipt_long',
    label: $localize`:Main navigation item:Movimenti`,
  },
  { path: '/budgets', icon: 'savings', label: $localize`:Main navigation item:Budget` },
  {
    path: '/accounts',
    icon: 'account_balance_wallet',
    label: $localize`:Main navigation item:Conti`,
  },
  { path: '/categories', icon: 'category', label: $localize`:Main navigation item:Categorie` },
];

/** Route of the quick-entry form, reachable from every page through the floating button. */
export const NEW_EXPENSE_PATH = '/transactions/new';
