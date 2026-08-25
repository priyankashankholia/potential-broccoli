import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ShopService } from './services/shop.service';
import { Shop } from './models/shop';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  private readonly shopService = inject(ShopService);

  shops = signal<Shop[]>([]);
  newShopName = '';
  errorMessage = '';

  constructor() {
    this.loadShops();
  }

  loadShops(): void {
    this.shopService.getShops().subscribe({
      next: (shops) => {
        console.log('SHOPS FROM API:', shops);
        this.shops.set(shops);
      },
      error: (error) => {
        console.error('SHOP API ERROR:', error);
        this.errorMessage = 'Unable to load shops.';
      }
    });
  }

  addShop(): void {
    const name = this.newShopName.trim();

    if (!name) {
      return;
    }

    this.shopService.createShop(name).subscribe({
      next: (shop) => {
        this.shops.update(current => [...current, shop]);
        this.newShopName = '';
      },
      error: (error) => {
        console.error('ADD SHOP ERROR:', error);
        this.errorMessage = 'Unable to add shop.';
      }
    });
  }
}