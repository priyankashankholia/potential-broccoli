import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class PaymentService {

  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/payments';

  // The API allocates the amount to the oldest collectable month first.
  recordPayment(request: {
    tenantId: number;
    amount: number;
    paymentDate?: string;
    paymentMode?: string;
    note?: string;
  }): Observable<unknown> {
    return this.http.post(this.apiUrl, request);
  }

  updatePayment(
    id: number,
    request: {
      amount: number;
      paymentDate?: string;
      paymentMode?: string;
      note?: string | null;
    }
  ): Observable<unknown> {
    return this.http.put(`${this.apiUrl}/${id}`, request);
  }

  deletePayment(id: number): Observable<unknown> {
    return this.http.delete(`${this.apiUrl}/${id}`);
  }
}
