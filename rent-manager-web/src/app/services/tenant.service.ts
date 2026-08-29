import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import {
  CreateTenantRequest,
  FirstRentOptions,
  Tenant,
  UpdateTenantRequest,
  UpdateTenantResponse
} from '../models/tenant';

@Injectable({ providedIn: 'root' })
export class TenantService {

  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/tenants';

  getTenants(): Observable<Tenant[]> {
    return this.http.get<Tenant[]>(this.apiUrl);
  }

  // Month labels and first due dates for the "first rent starts when"
  // choice. Worked out on the server so the wording matches the rent that
  // will actually be generated.
  getFirstRentOptions(rentDueDay: number): Observable<FirstRentOptions> {
    return this.http.get<FirstRentOptions>(
      `${this.apiUrl}/first-rent-options?rentDueDay=${rentDueDay}`
    );
  }

  createTenant(request: CreateTenantRequest): Observable<unknown> {
    return this.http.post(this.apiUrl, request);
  }

  updateTenant(
    id: number,
    request: UpdateTenantRequest
  ): Observable<UpdateTenantResponse> {
    return this.http.put<UpdateTenantResponse>(`${this.apiUrl}/${id}`, request);
  }

  deleteTenant(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
