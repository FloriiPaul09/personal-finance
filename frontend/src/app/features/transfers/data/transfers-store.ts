import { inject, Service } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import {
  CreateTransferRequest,
  DataChanges,
  TransferResponse,
  UpdateTransferRequest,
} from '../../../core';
import { TransfersApi } from './transfers-api';

/**
 * The transfers feature's only way to the API. Every write tells `DataChanges` that `transfers`
 * changed: balances and the movement list are read again.
 */
@Service()
export class TransfersStore {
  private readonly api = inject(TransfersApi);
  private readonly changes = inject(DataChanges);

  load(id: string): Promise<TransferResponse> {
    return firstValueFrom(this.api.get(id));
  }

  create(body: CreateTransferRequest): Promise<TransferResponse> {
    return this.write(this.api.create(body));
  }

  update(transfer: TransferResponse, body: UpdateTransferRequest): Promise<TransferResponse> {
    return this.write(this.api.update(transfer, body));
  }

  delete(transfer: TransferResponse): Promise<void> {
    return this.write(this.api.delete(transfer));
  }

  private async write<T>(request: Observable<T>): Promise<T> {
    try {
      return await firstValueFrom(request);
    } finally {
      this.changes.notify('transfers');
    }
  }
}
