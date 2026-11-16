import { computed, DestroyRef, Signal, signal } from '@angular/core';
import { Observable, Subscription } from 'rxjs';
import { PageResponse } from '../api/models';

/** A list read page by page with the API cursor (`{ items, nextCursor }`). */
export interface CursorPager<T> {
  /** Items of every page loaded so far, in the API order. */
  readonly items: Signal<readonly T[]>;
  /** A page is being loaded. */
  readonly loading: Signal<boolean>;
  /** Error of the last request (an `ApiError` for API errors); `undefined` when it succeeded. */
  readonly error: Signal<unknown>;
  /** The first page has arrived (until then `items` is empty because nothing is known). */
  readonly loaded: Signal<boolean>;
  /** There is a next page to load (always `true` before the first page). */
  readonly hasMore: Signal<boolean>;
  /** Loads the next page; ignored while a page is loading or after the last page. Retries after an error. */
  loadMore(): void;
  /** Forgets every page and loads the first one again, e.g. after the filters changed. */
  reset(): void;
}

/**
 * Creates a pager on `fetchPage(cursor)`, which reads the page after `cursor` (`null` = first page)
 * with the current filters. The pager starts empty: call `loadMore()` or `reset()` to load.
 *
 * - `reset()` cancels a request still running, so a page of the old filters never lands in the new list.
 * - The cursor is passed back unchanged; items created after the first page never shift the next ones.
 * - With `destroyRef`, the running request is cancelled when its owner is destroyed.
 */
export function cursorPager<T>(
  fetchPage: (cursor: string | null) => Observable<PageResponse<T>>,
  destroyRef?: DestroyRef,
): CursorPager<T> {
  const items = signal<readonly T[]>([]);
  const loading = signal(false);
  const error = signal<unknown>(undefined);
  const loaded = signal(false);
  const cursor = signal<string | null>(null);
  let running: Subscription | undefined;

  destroyRef?.onDestroy(() => running?.unsubscribe());

  function request(from: string | null): void {
    running?.unsubscribe();
    loading.set(true);
    error.set(undefined);
    running = fetchPage(from).subscribe({
      next: (page) => {
        items.update((current) => (from === null ? page.items : [...current, ...page.items]));
        cursor.set(page.nextCursor);
        loaded.set(true);
        loading.set(false);
      },
      error: (failure: unknown) => {
        error.set(failure);
        loading.set(false);
      },
    });
  }

  return {
    items: items.asReadonly(),
    loading: loading.asReadonly(),
    error: error.asReadonly(),
    loaded: loaded.asReadonly(),
    hasMore: computed(() => !loaded() || cursor() !== null),
    loadMore() {
      if (loading() || (loaded() && cursor() === null)) {
        return;
      }
      request(loaded() ? cursor() : null);
    },
    reset() {
      running?.unsubscribe();
      items.set([]);
      cursor.set(null);
      loaded.set(false);
      request(null);
    },
  };
}
