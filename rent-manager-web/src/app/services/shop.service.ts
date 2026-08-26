import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Shop } from '../models/shop';

@Injectable({
  providedIn: 'root'
})
export class ShopService {

  private readonly http = inject(HttpClient);

  private readonly apiUrl = '/api/shops';

  getShops(): Observable<Shop[]> {
    return this.http.get<Shop[]>(this.apiUrl);
  }

  getShop(id: number): Observable<Shop> {
    return this.http.get<Shop>(
      `${this.apiUrl}/${id}`
    );
  }

  createShop(name: string): Observable<Shop> {
    return this.http.post<Shop>(
      this.apiUrl,
      { name }
    );
  }

  updateShop(
    id: number,
    name: string
  ): Observable<Shop> {

    return this.http.put<Shop>(
      `${this.apiUrl}/${id}`,
      { name }
    );
  }

  deleteShop(id: number): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/${id}`
    );
  }
}