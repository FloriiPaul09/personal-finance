import { AccountType } from '../../../core';

export interface AccountTypeInfo {
  readonly type: AccountType;
  readonly label: string;
  /** Material Symbols name (docs/frontend/ui-components.md#icons). */
  readonly icon: string;
}

/** The types of account the API knows, in the order the form offers them. */
export const ACCOUNT_TYPES: readonly AccountTypeInfo[] = [
  {
    type: 'checking',
    label: $localize`:Account type:Conto corrente`,
    icon: 'account_balance',
  },
  { type: 'cash', label: $localize`:Account type:Contanti`, icon: 'payments' },
  { type: 'creditCard', label: $localize`:Account type:Carta di credito`, icon: 'credit_card' },
  { type: 'savings', label: $localize`:Account type:Risparmi`, icon: 'savings' },
  { type: 'other', label: $localize`:Account type:Altro`, icon: 'account_balance_wallet' },
];

export function accountTypeInfo(type: AccountType): AccountTypeInfo {
  return (
    ACCOUNT_TYPES.find((info) => info.type === type) ?? ACCOUNT_TYPES[ACCOUNT_TYPES.length - 1]
  );
}
