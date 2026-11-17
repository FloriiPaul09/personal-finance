import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateTransferRequest,
  ifMatch,
  TransferResponse,
  UpdateTransferRequest,
} from '../../../core';

/** The calls of the transfers API (docs/backend/api-endpoints.md#transfers). */
@Service()
export class TransfersApi {
  private readonly http = inject(HttpClient);

  get(id: string): Observable<TransferResponse> {
    return this.http.get<TransferResponse>(`/api/transfers/${id}`);
  }

  /** `body.id` is chosen by the app (UUIDv7): sending the same request again creates nothing new. */
  create(body: CreateTransferRequest): Observable<TransferResponse> {
    return this.http.post<TransferResponse>('/api/transfers', body);
  }

  update(transfer: TransferResponse, body: UpdateTransferRequest): Observable<TransferResponse> {
    return this.http.put<TransferResponse>(`/api/transfers/${transfer.id}`, body, {
      headers: ifMatch(transfer.version),
    });
  }

  delete(transfer: TransferResponse): Observable<void> {
    return this.http.delete<void>(`/api/transfers/${transfer.id}`, {
      headers: ifMatch(transfer.version),
    });
  }
}
