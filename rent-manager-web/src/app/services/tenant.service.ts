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

  getTenants(): Observable<Tenant[]> {
    return this.http.get<Tenant[]>(this.apiUrl);
  }

  getTenant(id: number): Observable<Tenant> {
    return this.http.get<Tenant>(
      `${this.apiUrl}/${id}`
    );
  }

  createTenant(
    tenant: Omit<Tenant, 'id'>
  ): Observable<Tenant> {

    return this.http.post<Tenant>(
      this.apiUrl,
      tenant
    );
  }

  updateTenant(
    id: number,
    tenant: Omit<Tenant, 'id'>
  ): Observable<Tenant> {

    return this.http.put<Tenant>(
      `${this.apiUrl}/${id}`,
      tenant
    );
  }

  deleteTenant(id: number): Observable<void> {

    return this.http.delete<void>(
      `${this.apiUrl}/${id}`
    );
  }
}