import { httpResource } from '@angular/common/http';
import { computed, inject, linkedSignal, Signal } from '@angular/core';
import { ApiError } from '../api/api-error';
import { ListResponse } from '../api/models';
import { DataChanges, DataResource } from './data-changes';

/** A small list read once and kept in memory (accounts, categories). */
export interface ReferenceList<T> {
  /** The last list received, kept while a newer one loads; `undefined` before the first. */
  readonly items: Signal<readonly T[] | undefined>;
  /** The error of the last read, if it failed (an `ApiError`). */
  readonly error: Signal<ApiError | undefined>;
  /** True only while the first list loads: later reads keep showing the last one. */
  readonly loading: Signal<boolean>;
  /** Reads the list again (e.g. "Riprova" after an error). */
  reload(): void;
}

/**
 * Reads `url` (a list that is not paged) and reads it again after every write to the resources
 * it `dependsOn` (`DataChanges`). The list stays on screen while it is read again, so a list does
 * not flash a progress bar after each archive or rename. Call it in an injection context.
 */
export function referenceList<T>(
  url: string,
  dependsOn: readonly DataResource[],
): ReferenceList<T> {
  const changes = inject(DataChanges);
  const resource = httpResource<ListResponse<T>>(() => {
    // A new request after each write: the running read, if any, is cancelled.
    changes.version(...dependsOn);
    return { url, params: { includeArchived: true } };
  });

  const items = linkedSignal<readonly T[] | undefined, readonly T[] | undefined>({
    source: () => (resource.hasValue() ? resource.value().items : undefined),
    computation: (received, previous) => received ?? previous?.value,
  });

  return {
    items,
    error: computed(() => resource.error() as ApiError | undefined),
    loading: computed(() => items() === undefined && resource.isLoading()),
    reload: () => void resource.reload(),
  };
}
