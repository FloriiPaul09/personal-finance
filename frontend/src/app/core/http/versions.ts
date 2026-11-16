import { HttpHeaders } from '@angular/common/http';
import { Version } from '../api/models';

/**
 * Header that every update and delete must send: `If-Match: "<version>"`, a strong entity tag
 * with the version read last (docs/backend/api-endpoints.md#versions). The version comes from the
 * body of any response, list items included, so an item can be changed from a list without
 * reading it again — and the PWA's offline queue will store it with each pending change.
 */
export function ifMatch(version: Version): HttpHeaders {
  if (!Number.isSafeInteger(version) || version < 0) {
    throw new RangeError(`A version is a non-negative integer, got ${version}.`);
  }
  return new HttpHeaders({ 'If-Match': `"${version}"` });
}
