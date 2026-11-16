import { v7 } from 'uuid';

/**
 * A new UUID version 7 for a resource the app creates (expenses, income, transfers).
 * Sending the id makes the create safe to repeat: the API answers `200` with the existing
 * resource instead of a duplicate, so the PWA's offline queue can replay a request as it is
 * (docs/backend/api-endpoints.md#transactions). Ids are time-ordered, also within the same
 * millisecond. `crypto.randomUUID()` is not enough: it makes version 4 ids, which the API refuses.
 */
export function newId(): string {
  return v7();
}
