import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { AccountRequest, AccountResponse, ifMatch } from '../../../core';

/** The writes of the accounts API (docs/backend/api-endpoints.md#accounts); reads are in core. */
@Service()
export class AccountsApi {
  private readonly http = inject(HttpClient);

  create(body: AccountRequest): Observable<AccountResponse> {
    return this.http.post<AccountResponse>('/api/accounts', body);
  }

  update(account: AccountResponse, body: AccountRequest): Observable<AccountResponse> {
    return this.http.put<AccountResponse>(`/api/accounts/${account.id}`, body, {
      headers: ifMatch(account.version),
    });
  }

  /** No version needed: archiving changes one flag and overwrites no edit. */
  archive(account: AccountResponse): Observable<AccountResponse> {
    return this.http.post<AccountResponse>(`/api/accounts/${account.id}/archive`, null);
  }

  unarchive(account: AccountResponse): Observable<AccountResponse> {
    return this.http.post<AccountResponse>(`/api/accounts/${account.id}/unarchive`, null);
  }

  delete(account: AccountResponse): Observable<void> {
    return this.http.delete<void>(`/api/accounts/${account.id}`, {
      headers: ifMatch(account.version),
    });
  }
}
