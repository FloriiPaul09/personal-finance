import { computed, inject, InjectionToken, linkedSignal, Service } from '@angular/core';
import { CategoryKind } from '../api/models';
import { SessionStore } from '../auth/session-store';

/** How many recent categories the quick entry offers as chips. */
export const RECENT_CATEGORIES = 6;

/** Where the choices are kept (tests replace it). `null` when the browser has no storage. */
export const RECENT_STORAGE = new InjectionToken<Storage | null>('RECENT_STORAGE', {
  factory: () => {
    try {
      return globalThis.localStorage ?? null;
    } catch {
      return null; // storage blocked (private mode of some browsers)
    }
  },
});

interface Recent {
  readonly expense: readonly string[];
  readonly income: readonly string[];
  readonly account: string | null;
}

const EMPTY: Recent = { expense: [], income: [], account: null };

/**
 * The last categories and account used on this device, so the quick entry offers them first
 * (docs/frontend/ui-components.md#quick-entry). Kept per user, in the browser's storage: a
 * convenience, not data; with no storage the app simply starts from nothing each time.
 * Ids that no longer exist or are archived are filtered out by the pages, not here.
 */
@Service()
export class RecentChoices {
  private readonly storage = inject(RECENT_STORAGE);
  private readonly session = inject(SessionStore);
  private readonly userId = computed(() => this.session.user()?.id ?? null);
  private readonly state = linkedSignal(() => this.read(this.userId()));

  /** Category ids of a kind, the most recent first. */
  categories(kind: CategoryKind): readonly string[] {
    return this.state()[kind];
  }

  /** The account of the last movement saved. */
  account(): string | null {
    return this.state().account;
  }

  /** A movement was saved with this category (if any) and account. */
  remember(choice: { kind?: CategoryKind; categoryId?: string | null; accountId: string }): void {
    const current = this.state();
    const { kind, categoryId } = choice;
    const next: Recent = {
      ...current,
      account: choice.accountId,
      ...(kind && categoryId
        ? {
            [kind]: [categoryId, ...current[kind].filter((id) => id !== categoryId)].slice(
              0,
              RECENT_CATEGORIES,
            ),
          }
        : {}),
    };
    this.state.set(next);
    this.write(next);
  }

  private key(userId: string): string {
    return `pf:${userId}:recent`;
  }

  private read(userId: string | null): Recent {
    if (!userId || !this.storage) {
      return EMPTY;
    }
    try {
      const saved = JSON.parse(this.storage.getItem(this.key(userId)) ?? 'null') as Partial<Recent>;
      return {
        expense: Array.isArray(saved?.expense) ? saved.expense.filter(isText) : [],
        income: Array.isArray(saved?.income) ? saved.income.filter(isText) : [],
        account: isText(saved?.account) ? saved.account : null,
      };
    } catch {
      return EMPTY;
    }
  }

  private write(recent: Recent): void {
    const userId = this.userId();
    if (!userId || !this.storage) {
      return;
    }
    try {
      this.storage.setItem(this.key(userId), JSON.stringify(recent));
    } catch {
      // Storage full or blocked: the choices just are not remembered.
    }
  }
}

function isText(value: unknown): value is string {
  return typeof value === 'string' && value.length > 0;
}
