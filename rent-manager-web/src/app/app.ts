import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

import { ShopService } from './services/shop.service';
import { TenantService } from './services/tenant.service';
import { Shop } from './models/shop';

interface Rent {
  id: number;
  tenantId: number;
  tenantName: string;
  year: number;
  month: number;
  amountDue: number;
  amountPaid: number;
  remaining: number;
  dueDate: string;
  isSettled: boolean;
}

interface Payment {
  id: number;
  rentId: number;
  amount: number;
  paymentDate: string;
  paymentMode: string;
  note: string | null;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [FormsModule, DatePipe],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  private readonly http = inject(HttpClient);
  private readonly shopService = inject(ShopService);
  private readonly tenantService = inject(TenantService);

  shops = signal<Shop[]>([]);
  rents = signal<Rent[]>([]);
  selectedRent = signal<Rent | null>(null);
  payments = signal<Payment[]>([]);

  selectedShopId: number | null = null;
  selectedTenantId: number | null = null;

  paymentType: 'full' | 'partial' = 'full';
  paymentAmount = 0;

  tenantName = '';
  mobileNumber = '';
  panCard = '';
  monthlyRent = 0;
  rentDueDay = 1;
  securityDeposit: number | null = null;

  message = '';
  errorMessage = '';

  get occupiedCount(): number {
    return this.shops().filter(shop => shop.isOccupied).length;
  }

  get vacantCount(): number {
    return this.shops().filter(shop => !shop.isOccupied).length;
  }

  get totalOutstanding(): number {
    return this.rents().reduce(
      (total, rent) => total + rent.remaining,
      0
    );
  }

  constructor() {
    this.loadShops();
    this.ensureCurrentMonthRent();
  }

  ensureCurrentMonthRent(): void {
    const now = new Date();

    const year = now.getFullYear();
    const month = now.getMonth() + 1;

    this.http.post(
      `/api/rents/generate/${year}/${month}`,
      {}
    ).subscribe({
      next: result => {
        console.log(
          `Monthly rent check completed for ${month}/${year}:`,
          result
        );

        this.loadRents();
      },
      error: error => {
        console.error(
          'Unable to generate monthly rent:',
          error
        );
      }
    });
  }

  loadShops(): void {
    this.shopService.getShops().subscribe({
      next: shops => {
        this.shops.set(shops);
      },
      error: () => {
        this.errorMessage = 'Unable to load shops.';
      }
    });

    this.loadRents();
  }

  loadRents(): void {
    this.http.get<Rent[]>('/api/rents').subscribe({
      next: rents => {
        this.rents.set(rents);
      },
      error: () => {
        this.errorMessage =
          'Unable to load rent information.';
      }
    });
  }

  showTenantForm(shopId: number): void {
    this.selectedShopId = shopId;
    this.selectedTenantId = null;
    this.selectedRent.set(null);
    this.payments.set([]);
    this.clearMessages();
  }

  manageTenant(tenantId: number): void {
    this.selectedTenantId = tenantId;
    this.selectedShopId = null;
    this.selectedRent.set(null);
    this.payments.set([]);
    this.clearMessages();

    this.loadRent(tenantId);
  }

  loadRent(tenantId: number): void {
    this.http.get<Rent[]>('/api/rents').subscribe({
      next: rents => {
        this.rents.set(rents);

        const tenantRents = rents
          .filter(item => item.tenantId === tenantId)
          .sort((a, b) => {
            const dateA = new Date(a.dueDate).getTime();
            const dateB = new Date(b.dueDate).getTime();

            return dateB - dateA;
          });

        const rent = tenantRents[0];

        if (!rent) {
          this.selectedRent.set(null);
          this.payments.set([]);
          this.errorMessage =
            'No rent record found for this tenant.';
          return;
        }

        this.selectedRent.set(rent);
        this.paymentAmount = rent.remaining;

        this.loadPayments(rent.id);
      },
      error: () => {
        this.errorMessage = 'Unable to load rent.';
      }
    });
  }

  loadPayments(rentId: number): void {
    console.log(
      'Loading payments for rent:',
      rentId
    );

    this.http.get<Payment[]>(
      `/api/payments/rent/${rentId}`
    ).subscribe({
      next: payments => {
        console.log(
          'Payments loaded:',
          payments
        );

        this.payments.set(payments);
      },
      error: error => {
        console.error(
          'Payment history error:',
          error
        );

        this.payments.set([]);

        this.errorMessage =
          'Unable to load payment history.';
      }
    });
  }

  selectFullPayment(): void {
    this.paymentType = 'full';

    const rent = this.selectedRent();

    if (rent) {
      this.paymentAmount = rent.remaining;
    }
  }

  selectPartialPayment(): void {
    this.paymentType = 'partial';
    this.paymentAmount = 0;
  }

  markPayment(): void {
    const rent = this.selectedRent();

    if (!rent) {
      this.errorMessage = 'Rent not found.';
      return;
    }

    if (rent.isSettled || rent.remaining <= 0) {
      this.errorMessage =
        'This rent has already been fully paid.';
      return;
    }

    const amount =
      this.paymentType === 'full'
        ? rent.remaining
        : Number(this.paymentAmount);

    if (!Number.isFinite(amount) || amount <= 0) {
      this.errorMessage =
        'Enter a valid payment amount.';
      return;
    }

    if (amount > rent.remaining) {
      this.errorMessage =
        `Payment cannot exceed the remaining balance of ₹${rent.remaining}.`;
      return;
    }

    this.http.post<any>('/api/payments', {
      rentId: rent.id,
      amount,
      paymentMode: 'Cash',
      note:
        this.paymentType === 'full'
          ? 'Full cash payment'
          : 'Partial cash payment'
    }).subscribe({
      next: payment => {
        this.message =
          `Payment of ₹${payment.amount} recorded successfully.`;

        this.loadRent(rent.tenantId);
        this.loadRents();
      },
      error: error => {
        this.errorMessage =
          error?.error ||
          'Unable to save payment.';
      }
    });
  }

  sendWhatsAppMessage(): void {
    const rent = this.selectedRent();

    if (!rent) {
      this.errorMessage =
        'Rent information is not available.';
      return;
    }

    const shop = this.shops().find(
      shop => shop.tenant?.id === rent.tenantId
    );

    const mobileNumber =
      shop?.tenant?.mobileNumber;

    if (!mobileNumber) {
      this.errorMessage =
        'Tenant mobile number is not available.';
      return;
    }

    const monthName = new Date(
      rent.year,
      rent.month - 1,
      1
    ).toLocaleString('en-US', {
      month: 'long'
    });

    const text = rent.isSettled
      ? `Rent payment received. Your rent for ${monthName} ${rent.year} is fully paid. Total paid: ₹${rent.amountPaid}. Thank you.`
      : `Rent payment received. Total paid for ${monthName} ${rent.year}: ₹${rent.amountPaid}. Remaining balance: ₹${rent.remaining}.`;

    let cleanNumber =
      mobileNumber.replace(/\D/g, '');

    if (cleanNumber.length === 10) {
      cleanNumber = `91${cleanNumber}`;
    }

    const whatsappUrl =
      `https://wa.me/${cleanNumber}?text=${encodeURIComponent(text)}`;

    window.open(
      whatsappUrl,
      '_blank',
      'noopener,noreferrer'
    );
  }

  addTenant(): void {
    if (
      !this.selectedShopId ||
      !this.tenantName.trim() ||
      !this.mobileNumber.trim() ||
      this.monthlyRent <= 0
    ) {
      this.errorMessage =
        'Name, mobile number, shop and monthly rent are required.';
      return;
    }

    this.tenantService.createTenant({
      name: this.tenantName.trim(),
      mobileNumber: this.mobileNumber.trim(),
      panCard:
        this.panCard.trim() || undefined,
      monthlyRent: this.monthlyRent,
      rentDueDay: this.rentDueDay,
      securityDeposit:
        this.securityDeposit ?? undefined,
      shopId: this.selectedShopId
    }).subscribe({
      next: () => {
        this.message =
          'Tenant added successfully.';

        this.selectedShopId = null;
        this.resetTenantForm();

        this.loadShops();
        this.loadRents();
      },
      error: error => {
        this.errorMessage =
          error?.error ||
          'Unable to add tenant.';
      }
    });
  }

  cancelTenant(): void {
    this.selectedShopId = null;
    this.resetTenantForm();
    this.clearMessages();
  }

  closeTenant(): void {
    this.selectedTenantId = null;
    this.selectedRent.set(null);
    this.payments.set([]);
    this.clearMessages();
  }

  private resetTenantForm(): void {
    this.tenantName = '';
    this.mobileNumber = '';
    this.panCard = '';
    this.monthlyRent = 0;
    this.rentDueDay = 1;
    this.securityDeposit = null;
  }

  private clearMessages(): void {
    this.message = '';
    this.errorMessage = '';
  }
}