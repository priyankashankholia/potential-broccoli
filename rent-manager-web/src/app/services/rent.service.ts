import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { TenantLedger } from '../models/rent';

@Injectable({ providedIn: 'root' })
export class RentService {

  private readonly http = inject(HttpClient);

  // One request returns the current month, the cumulative payable and the
  // full history with payments attached. There is no generate call any
  // more, the API creates each month on its own.
  getLedger(tenantId: number): Observable<TenantLedger> {
    return this.http.get<TenantLedger>(`/api/rents/tenant/${tenantId}/ledger`);
  }
}
