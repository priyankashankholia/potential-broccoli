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

  createShop(name: string): Observable<Shop> {
    return this.http.post<Shop>(this.apiUrl, {
      name
    });
  }
}
