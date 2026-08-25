import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Tenant } from '../models/tenant';

@Injectable({
  providedIn: 'root'
})
export class TenantService {
  private readonly http = inject(HttpClient);

  private readonly apiUrl = '/api/tenants';

  createTenant(tenant: Omit<Tenant, 'id'>): Observable<Tenant> {
    return this.http.post<Tenant>(this.apiUrl, tenant);
  }
}
