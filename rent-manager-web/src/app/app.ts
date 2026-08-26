import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

import { ShopService } from './services/shop.service';
import { TenantService } from './services/tenant.service';
import { Shop } from './models/shop';
import { Tenant } from './models/tenant';

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

interface Notification {
  id: number;
  tenantId: number;
  tenantName: string;
  rentId: number | null;
  type: string;
  channel: string;
  message: string;
  status: string;
  createdAt: string;
  sentAt: string | null;
}

type DashboardFilter =
  | 'all'
  | 'occupied'
  | 'vacant'
  | 'outstanding';

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
  notifications = signal<Notification[]>([]);

  selectedRent = signal<Rent | null>(null);
  payments = signal<Payment[]>([]);

  selectedShopId: number | null = null;
  selectedTenantId: number | null = null;

  activeFilter: DashboardFilter = 'all';

  // =========================
  // SHOP FORM
  // =========================

  showShopForm = false;
  editingShopId: number | null = null;
  shopName = '';

  // =========================
  // TENANT FORM
  // =========================

  showTenantEditForm = false;
  editingTenantId: number | null = null;

  tenantName = '';
  mobileNumber = '';
  panCard = '';
  monthlyRent = 0;
  rentDueDay = 1;
  securityDeposit: number | null = null;
  tenantShopId: number | null = null;

  // =========================
  // PAYMENT
  // =========================

  paymentType: 'full' | 'partial' = 'full';
  paymentAmount = 0;

  // =========================
  // MESSAGES
  // =========================

  message = '';
  errorMessage = '';

  constructor() {
    this.loadShops();
    this.loadNotifications();
    this.ensureCurrentMonthRent();
  }

  // =========================
  // DASHBOARD
  // =========================

  get occupiedCount(): number {
    return this.shops()
      .filter(shop => shop.isOccupied)
      .length;
  }

  get vacantCount(): number {
    return this.shops()
      .filter(shop => !shop.isOccupied)
      .length;
  }

  get totalOutstanding(): number {
    return this.rents()
      .reduce(
        (total, rent) => total + rent.remaining,
        0
      );
  }

  get filteredShops(): Shop[] {

    switch (this.activeFilter) {

      case 'occupied':
        return this.shops()
          .filter(shop => shop.isOccupied);

      case 'vacant':
        return this.shops()
          .filter(shop => !shop.isOccupied);

      case 'outstanding':
        return this.shops()
          .filter(shop =>
            !!shop.tenant &&
            this.getShopOutstanding(shop) > 0
          );

      default:
        return this.shops();
    }
  }

  setFilter(filter: DashboardFilter): void {
    this.activeFilter = filter;
    this.clearMessages();
  }

  // =========================
  // SHOPS
  // =========================

  loadShops(): void {

    this.shopService.getShops()
      .subscribe({

        next: shops => {
          this.shops.set(shops);
        },

        error: () => {
          this.errorMessage =
            'Unable to load shops.';
        }

      });

    this.loadRents();
  }

  openAddShop(): void {

    this.editingShopId = null;
    this.shopName = '';
    this.showShopForm = true;

    this.clearMessages();
  }

  openEditShop(shop: Shop): void {

    if (shop.isOccupied) {

      this.errorMessage =
        'Cannot edit an occupied shop.';

      return;
    }

    this.editingShopId = shop.id;
    this.shopName = shop.name;
    this.showShopForm = true;

    this.clearMessages();
  }

  cancelShopForm(): void {

    this.showShopForm = false;
    this.editingShopId = null;
    this.shopName = '';
  }

  saveShop(): void {

    const name = this.shopName.trim();

    if (!name) {

      this.errorMessage =
        'Shop name is required.';

      return;
    }

    if (this.editingShopId !== null) {

      this.shopService
        .updateShop(
          this.editingShopId,
          name
        )
        .subscribe({

          next: () => {

            this.message =
              'Shop updated successfully.';

            this.cancelShopForm();
            this.loadShops();
          },

          error: error => {

            this.errorMessage =
              error?.error ||
              'Unable to update shop.';
          }

        });

      return;
    }

    this.shopService
      .createShop(name)
      .subscribe({

        next: () => {

          this.message =
            'Shop added successfully.';

          this.cancelShopForm();
          this.loadShops();
        },

        error: error => {

          this.errorMessage =
            error?.error ||
            'Unable to add shop.';
        }

      });
  }

  deleteShop(shop: Shop): void {

    if (shop.isOccupied) {

      this.errorMessage =
        'Cannot delete a shop while a tenant is assigned.';

      return;
    }

    const confirmed =
      window.confirm(
        `Delete "${shop.name}"? This cannot be undone.`
      );

    if (!confirmed) {
      return;
    }

    this.shopService
      .deleteShop(shop.id)
      .subscribe({

        next: () => {

          this.message =
            'Shop deleted successfully.';

          this.loadShops();
        },

        error: error => {

          this.errorMessage =
            error?.error ||
            'Unable to delete shop.';
        }

      });
  }

  // =========================
  // RENT GENERATION
  // =========================

  ensureCurrentMonthRent(): void {

    const now = new Date();

    const year = now.getFullYear();
    const month = now.getMonth() + 1;

    this.http.post(
      `/api/rents/generate/${year}/${month}`,
      {}
    )
    .subscribe({

      next: () => {

        this.loadRents();
        this.loadNotifications();

      },

      error: error => {

        console.error(
          'Unable to generate monthly rent:',
          error
        );

      }

    });
  }

  // =========================
  // RENTS
  // =========================

  loadRents(): void {

    this.http
      .get<Rent[]>('/api/rents')
      .subscribe({

        next: rents => {
          this.rents.set(rents);
        },

        error: () => {

          this.errorMessage =
            'Unable to load rent information.';
        }

      });
  }

  getCurrentRentForTenant(
    tenantId: number | undefined
  ): Rent | null {

    if (!tenantId) {
      return null;
    }

    const tenantRents =
      this.rents()
        .filter(
          rent =>
            rent.tenantId === tenantId
        )
        .sort(
          (a, b) =>
            new Date(b.dueDate).getTime() -
            new Date(a.dueDate).getTime()
        );

    return tenantRents[0] ?? null;
  }

  getShopRent(shop: Shop): number {

    const rent =
      this.getCurrentRentForTenant(
        shop.tenant?.id
      );

    return (
      rent?.amountDue ??
      shop.tenant?.monthlyRent ??
      0
    );
  }

  getShopPaid(shop: Shop): number {

    const rent =
      this.getCurrentRentForTenant(
        shop.tenant?.id
      );

    return rent?.amountPaid ?? 0;
  }

  getShopOutstanding(shop: Shop): number {

    const rent =
      this.getCurrentRentForTenant(
        shop.tenant?.id
      );

    return rent?.remaining ?? 0;
  }

  // =========================
  // ADD TENANT
  // =========================

  showTenantForm(shopId: number): void {

    this.selectedShopId = shopId;

    this.selectedTenantId = null;

    this.selectedRent.set(null);
    this.payments.set([]);

    this.showTenantEditForm = false;

    this.resetTenantForm();

    this.clearMessages();
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

    if (
      this.rentDueDay < 1 ||
      this.rentDueDay > 31
    ) {

      this.errorMessage =
        'Rent due day must be between 1 and 31.';

      return;
    }

    this.tenantService
      .createTenant({

        name:
          this.tenantName.trim(),

        mobileNumber:
          this.mobileNumber.trim(),

        panCard:
          this.panCard.trim() ||
          undefined,

        monthlyRent:
          this.monthlyRent,

        rentDueDay:
          this.rentDueDay,

        securityDeposit:
          this.securityDeposit ??
          undefined,

        shopId:
          this.selectedShopId

      })
      .subscribe({

        next: () => {

          this.message =
            'Tenant added successfully.';

          this.selectedShopId = null;

          this.resetTenantForm();

          this.loadShops();
          this.loadRents();
          this.loadNotifications();

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

  // =========================
  // EDIT TENANT
  // =========================

  openEditTenant(
    shop: Shop
  ): void {

    if (!shop.tenant) {

      this.errorMessage =
        'No tenant is assigned to this shop.';

      return;
    }

    const tenant = shop.tenant;

    this.editingTenantId =
      tenant.id;

    this.tenantName =
      tenant.name;

    this.mobileNumber =
      tenant.mobileNumber;

    this.panCard =
      tenant.panCard ?? '';

    this.monthlyRent =
      tenant.monthlyRent ?? 0;

    this.rentDueDay =
      tenant.rentDueDay ?? 1;

    this.securityDeposit =
      tenant.securityDeposit ?? null;

    this.tenantShopId =
      shop.id;

    this.showTenantEditForm = true;

    this.selectedShopId = null;

    this.clearMessages();
  }

  cancelTenantEdit(): void {

    this.showTenantEditForm = false;

    this.editingTenantId = null;

    this.tenantShopId = null;

    this.resetTenantForm();

    this.clearMessages();
  }

  updateTenant(): void {

    if (
      this.editingTenantId === null
    ) {

      this.errorMessage =
        'Tenant not selected.';

      return;
    }

    if (
      !this.tenantName.trim() ||
      !this.mobileNumber.trim()
    ) {

      this.errorMessage =
        'Tenant name and mobile number are required.';

      return;
    }

    if (
      this.monthlyRent <= 0
    ) {

      this.errorMessage =
        'Monthly rent must be greater than zero.';

      return;
    }

    if (
      this.rentDueDay < 1 ||
      this.rentDueDay > 31
    ) {

      this.errorMessage =
        'Rent due day must be between 1 and 31.';

      return;
    }

    if (!this.tenantShopId) {

      this.errorMessage =
        'Tenant shop is required.';

      return;
    }

    this.tenantService
      .updateTenant(
        this.editingTenantId,
        {
          name:
            this.tenantName.trim(),

          mobileNumber:
            this.mobileNumber.trim(),

          panCard:
            this.panCard.trim() ||
            undefined,

          monthlyRent:
            this.monthlyRent,

          rentDueDay:
            this.rentDueDay,

          securityDeposit:
            this.securityDeposit ??
            undefined,

          shopId:
            this.tenantShopId
        }
      )
      .subscribe({

        next: () => {

          this.message =
            'Tenant updated successfully.';

          this.cancelTenantEdit();

          this.loadShops();
          this.loadRents();

        },

        error: error => {

          this.errorMessage =
            error?.error ||
            'Unable to update tenant.';
        }

      });
  }

  // =========================
  // REMOVE TENANT
  // =========================

  removeTenant(
    shop: Shop
  ): void {

    const tenant =
      shop.tenant;

    if (!tenant) {

      this.errorMessage =
        'No tenant is assigned to this shop.';

      return;
    }

    const confirmed =
      window.confirm(
        `Remove ${tenant.name} from ${shop.name}?`
      );

    if (!confirmed) {
      return;
    }

    this.tenantService
      .deleteTenant(
        tenant.id
      )
      .subscribe({

        next: () => {

          this.message =
            `${tenant.name} was removed successfully.`;

          this.selectedTenantId = null;

          this.selectedRent.set(null);

          this.payments.set([]);

          this.loadShops();
          this.loadRents();
          this.loadNotifications();

        },

        error: error => {

          this.errorMessage =
            error?.error ||
            'Unable to remove tenant.';
        }

      });
  }

  private resetTenantForm(): void {

    this.tenantName = '';

    this.mobileNumber = '';

    this.panCard = '';

    this.monthlyRent = 0;

    this.rentDueDay = 1;

    this.securityDeposit = null;

    this.tenantShopId = null;
  }

  // =========================
  // MANAGE RENT
  // =========================

  manageTenant(
    tenantId: number
  ): void {

    this.selectedTenantId =
      tenantId;

    this.selectedShopId = null;

    this.showTenantEditForm = false;

    this.selectedRent.set(null);

    this.payments.set([]);

    this.clearMessages();

    this.loadRent(tenantId);
  }

  loadRent(
    tenantId: number
  ): void {

    this.http
      .get<Rent[]>('/api/rents')
      .subscribe({

        next: rents => {

          this.rents.set(rents);

          const tenantRents =
            rents
              .filter(
                rent =>
                  rent.tenantId === tenantId
              )
              .sort(
                (a, b) =>
                  new Date(b.dueDate).getTime() -
                  new Date(a.dueDate).getTime()
              );

          const rent =
            tenantRents[0];

          if (!rent) {

            this.selectedRent.set(null);

            this.payments.set([]);

            this.errorMessage =
              'No rent record found for this tenant.';

            return;
          }

          this.selectedRent.set(rent);

          this.paymentAmount =
            rent.remaining;

          this.paymentType = 'full';

          this.loadPayments(
            rent.id
          );
        },

        error: () => {

          this.errorMessage =
            'Unable to load rent.';
        }

      });
  }

  loadPayments(
    rentId: number
  ): void {

    this.http
      .get<Payment[]>(
        `/api/payments/rent/${rentId}`
      )
      .subscribe({

        next: payments => {

          this.payments.set(
            payments
          );
        },

        error: () => {

          this.payments.set([]);

          this.errorMessage =
            'Unable to load payment history.';
        }

      });
  }

  selectFullPayment(): void {

    this.paymentType =
      'full';

    const rent =
      this.selectedRent();

    if (rent) {

      this.paymentAmount =
        rent.remaining;
    }
  }

  selectPartialPayment(): void {

    this.paymentType =
      'partial';

    this.paymentAmount = 0;
  }

  markPayment(): void {

    const rent =
      this.selectedRent();

    if (!rent) {

      this.errorMessage =
        'Rent not found.';

      return;
    }

    if (
      rent.isSettled ||
      rent.remaining <= 0
    ) {

      this.errorMessage =
        'This rent has already been fully paid.';

      return;
    }

    const amount =
      this.paymentType === 'full'
        ? rent.remaining
        : Number(this.paymentAmount);

    if (
      !Number.isFinite(amount) ||
      amount <= 0
    ) {

      this.errorMessage =
        'Enter a valid payment amount.';

      return;
    }

    if (
      amount > rent.remaining
    ) {

      this.errorMessage =
        `Payment cannot exceed the remaining balance of ₹${rent.remaining}.`;

      return;
    }

    this.http
      .post<any>(
        '/api/payments',
        {
          rentId: rent.id,
          amount,
          paymentMode: 'Cash',
          note:
            this.paymentType === 'full'
              ? 'Full cash payment'
              : 'Partial cash payment'
        }
      )
      .subscribe({

        next: payment => {

          this.message =
            `Payment of ₹${payment.amount} recorded successfully.`;

          this.loadRent(
            rent.tenantId
          );

          this.loadRents();
          this.loadNotifications();

        },

        error: error => {

          this.errorMessage =
            error?.error ||
            'Unable to save payment.';
        }

      });
  }

  closeTenant(): void {

    this.selectedTenantId = null;

    this.selectedRent.set(null);

    this.payments.set([]);

    this.clearMessages();
  }

  // =========================
  // NOTIFICATIONS
  // =========================

  loadNotifications(): void {

    this.http
      .get<Notification[]>(
        '/api/notifications'
      )
      .subscribe({

        next: notifications => {

          this.notifications.set(
            notifications
          );
        },

        error: () => {

          this.errorMessage =
            'Unable to load notifications.';
        }

      });
  }

  generateReminders(): void {

    this.clearMessages();

    this.http
      .post<{ created: number }>(
        '/api/reminders/generate',
        {}
      )
      .subscribe({

        next: result => {

          this.message =
            result.created === 0
              ? 'No new rent reminders were required.'
              : `${result.created} rent reminder${
                  result.created === 1
                    ? ''
                    : 's'
                } created.`;

          this.loadNotifications();
        },

        error: error => {

          this.errorMessage =
            error?.error ||
            'Unable to generate rent reminders.';
        }

      });
  }

  processNotifications(): void {

    this.clearMessages();

    this.http
      .post<{ processed: number }>(
        '/api/notification-delivery/process',
        {}
      )
      .subscribe({

        next: result => {

          this.message =
            result.processed === 0
              ? 'No pending notifications.'
              : `${result.processed} notification${
                  result.processed === 1
                    ? ''
                    : 's'
                } processed.`;

          this.loadNotifications();
        },

        error: error => {

          this.errorMessage =
            error?.error ||
            'Unable to process notifications.';
        }

      });
  }

  // =========================
  // TEMP WHATSAPP
  // =========================

  sendWhatsAppMessage(): void {

    const rent =
      this.selectedRent();

    if (!rent) {

      this.errorMessage =
        'Rent information is not available.';

      return;
    }

    const shop =
      this.shops().find(
        shop =>
          shop.tenant?.id ===
          rent.tenantId
      );

    const mobileNumber =
      shop?.tenant?.mobileNumber;

    if (!mobileNumber) {

      this.errorMessage =
        'Tenant mobile number is not available.';

      return;
    }

    const monthName =
      new Date(
        rent.year,
        rent.month - 1,
        1
      ).toLocaleString(
        'en-US',
        {
          month: 'long'
        }
      );

    const text =
      rent.isSettled
        ? `Rent payment received. Your rent for ${monthName} ${rent.year} is fully paid. Total paid: ₹${rent.amountPaid}. Thank you.`
        : `Rent payment received. Total paid for ${monthName} ${rent.year}: ₹${rent.amountPaid}. Remaining balance: ₹${rent.remaining}.`;

    let cleanNumber =
      mobileNumber.replace(
        /\D/g,
        ''
      );

    if (
      cleanNumber.length === 10
    ) {

      cleanNumber =
        `91${cleanNumber}`;
    }

    const whatsappUrl =
      `https://wa.me/${cleanNumber}?text=${encodeURIComponent(text)}`;

    window.open(
      whatsappUrl,
      '_blank',
      'noopener,noreferrer'
    );
  }

  // =========================
  // CLEAR MESSAGES
  // =========================

  private clearMessages(): void {

    this.message = '';
    this.errorMessage = '';
  }
}