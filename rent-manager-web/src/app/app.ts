import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { apiErrorMessage } from './services/api-error';
import { AuthService } from './services/auth.service';
import { PaymentService } from './services/payment.service';
import { PushService } from './services/push.service';
import { RentService } from './services/rent.service';
import { ShopService } from './services/shop.service';
import { TenantService } from './services/tenant.service';

import { FirstRentOptions } from './models/tenant';
import { Payment, RentStatus, TenantLedger } from './models/rent';
import { Shop } from './models/shop';

type DashboardFilter = 'all' | 'occupied' | 'vacant' | 'outstanding';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {

  private readonly auth = inject(AuthService);
  readonly push = inject(PushService);
  private readonly shopService = inject(ShopService);
  private readonly tenantService = inject(TenantService);
  private readonly rentService = inject(RentService);
  private readonly paymentService = inject(PaymentService);

  // ---------------------------------------------------------------
  // Authentication
  // ---------------------------------------------------------------

  readonly isAuthenticated = this.auth.isAuthenticated;
  readonly displayName = this.auth.displayName;

  loginUsername = '';
  loginPassword = '';
  loginError = signal('');
  loggingIn = signal(false);

  constructor() {
    if (this.isAuthenticated()) {
      this.loadShops();
      this.push.refresh();
    }
  }

  async toggleNotifications(): Promise<void> {
    if (this.push.enabled()) {
      await this.push.disable();
      return;
    }

    const error = await this.push.enable();

    if (error) {
      this.showPopup(error);
    }
  }

  login(): void {
    const username = this.loginUsername.trim();

    if (!username || !this.loginPassword) {
      this.loginError.set('Please enter your username and password.');
      return;
    }

    this.loginError.set('');
    this.loggingIn.set(true);

    this.auth.login(username, this.loginPassword).subscribe({
      next: () => {
        this.loggingIn.set(false);
        this.loginPassword = '';
        this.loadShops();
      },
      error: error => {
        this.loggingIn.set(false);
        this.loginPassword = '';
        this.loginError.set(
          apiErrorMessage(error, 'Unable to sign in. Please try again.')
        );
      }
    });
  }

  logout(): void {
    this.auth.logout();

    this.shops.set([]);
    this.ledger.set(null);
    this.selectedTenantId.set(null);
    this.closeAllModals();

    this.loginUsername = '';
    this.loginPassword = '';
    this.loginError.set('');
  }

  // ---------------------------------------------------------------
  // Dashboard
  // ---------------------------------------------------------------

  readonly shops = signal<Shop[]>([]);
  readonly ledger = signal<TenantLedger | null>(null);
  readonly selectedTenantId = signal<number | null>(null);
  readonly loadingShops = signal(false);

  activeFilter: DashboardFilter = 'all';

  message = signal('');

  readonly popupMessage = signal('');
  readonly popupTitle = signal('Action could not be completed');

  get todayDisplay(): string {
    return new Date().toLocaleDateString('en-IN', {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric'
    });
  }

  readonly occupiedCount = computed(
    () => this.shops().filter(s => s.isOccupied).length
  );

  readonly vacantCount = computed(
    () => this.shops().filter(s => !s.isOccupied).length
  );

  readonly totalOutstanding = computed(
    () => this.shops().reduce((total, shop) => total + (shop.rent?.totalPayable ?? 0), 0)
  );

  get filteredShops(): Shop[] {
    switch (this.activeFilter) {
      case 'occupied':
        return this.shops().filter(s => s.isOccupied);

      case 'vacant':
        return this.shops().filter(s => !s.isOccupied);

      case 'outstanding':
        return this.shops().filter(s => (s.rent?.totalPayable ?? 0) > 0);

      default:
        return this.shops();
    }
  }

  setFilter(filter: DashboardFilter): void {
    this.activeFilter = filter;
    this.message.set('');
  }

  loadShops(): void {
    this.loadingShops.set(true);

    this.shopService.getShops().subscribe({
      next: shops => {
        this.shops.set(shops);
        this.loadingShops.set(false);
      },
      error: error => {
        this.loadingShops.set(false);

        if ((error?.status ?? 0) !== 401) {
          this.showPopup(apiErrorMessage(error, 'Unable to load shops.'));
        }
      }
    });
  }

  // ---------------------------------------------------------------
  // Display helpers
  // ---------------------------------------------------------------

  // The API sends plain calendar dates like "2026-08-30". Parsing those
  // with new Date() applies a timezone offset, which is the bug that made
  // the day counts wrong. Format the string directly instead.
  formatDate(value: string | null | undefined): string {
    if (!value) {
      return '-';
    }

    const [year, month, day] = value.slice(0, 10).split('-').map(Number);

    if (!year || !month || !day) {
      return '-';
    }

    return new Date(year, month - 1, day).toLocaleDateString('en-IN', {
      day: 'numeric',
      month: 'short',
      year: 'numeric'
    });
  }

  money(value: number | null | undefined): string {
    return (value ?? 0).toLocaleString('en-IN');
  }

  statusClass(status: RentStatus | string | undefined): string {
    switch (status) {
      case 'Paid': return 'paid';
      case 'Outstanding': return 'outstanding';
      case 'Pending': return 'pending';
      default: return 'upcoming';
    }
  }

  statusLabel(status: RentStatus | string | undefined): string {
    return status === 'Paid' ? 'Paid this month' : (status ?? 'Upcoming');
  }

  // ---------------------------------------------------------------
  // Shop form
  // ---------------------------------------------------------------

  showShopForm = signal(false);
  editingShopId: number | null = null;
  shopName = '';
  // Change password
  showPasswordForm = signal(false);
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  passwordError = signal('');
  savingPassword = signal(false);

  openPasswordForm(): void {
    this.currentPassword = '';
    this.newPassword = '';
    this.confirmPassword = '';
    this.passwordError.set('');
    this.showPasswordForm.set(true);
  }

  cancelPasswordForm(): void {
    this.showPasswordForm.set(false);
    this.passwordError.set('');
  }

  savePassword(): void {
    this.passwordError.set('');

    if (!this.currentPassword) {
      this.passwordError.set('Enter your current password.');
      return;
    }

    if (this.newPassword.trim().length < 8) {
      this.passwordError.set('New password must be at least 8 characters.');
      return;
    }

    if (this.newPassword !== this.confirmPassword) {
      this.passwordError.set('The two new passwords do not match.');
      return;
    }

    if (this.newPassword === this.currentPassword) {
      this.passwordError.set('The new password must be different.');
      return;
    }

    this.savingPassword.set(true);

    this.auth.changePassword(this.currentPassword, this.newPassword).subscribe({
      next: () => {
        this.savingPassword.set(false);
        this.showPasswordForm.set(false);
        this.showPopup('Password changed. Use the new one next time you sign in.');
      },
      error: (error) => {
        this.savingPassword.set(false);
        this.passwordError.set(
          apiErrorMessage(error, 'Unable to change the password.')
        );
      }
    });
  }

  shopFormError = signal('');
  savingShop = signal(false);

  openAddShop(): void {
    this.editingShopId = null;
    this.shopName = '';
    this.shopFormError.set('');
    this.showShopForm.set(true);
    this.message.set('');
  }

  openEditShop(shop: Shop): void {
    this.editingShopId = shop.id;
    this.shopName = shop.name;
    this.shopFormError.set('');
    this.showShopForm.set(true);
    this.message.set('');
  }

  cancelShopForm(): void {
    this.showShopForm.set(false);
    this.editingShopId = null;
    this.shopName = '';
    this.shopFormError.set('');
  }

  saveShop(): void {
    const name = this.shopName.trim();

    this.shopFormError.set('');

    if (!name) {
      this.shopFormError.set('Shop name is required.');
      return;
    }

    const wasEditing = this.editingShopId !== null;

    const request = this.editingShopId === null
      ? this.shopService.createShop(name)
      : this.shopService.updateShop(this.editingShopId, name);

    this.savingShop.set(true);

    request.subscribe({
      next: () => {
        this.savingShop.set(false);
        this.message.set(wasEditing ? 'Shop updated.' : 'Shop added.');
        this.cancelShopForm();
        this.loadShops();
      },
      // A duplicate name comes back as 409. Shown here so the modal stays
      // open and the name can be corrected.
      error: error => {
        this.savingShop.set(false);
        this.shopFormError.set(apiErrorMessage(error, 'Unable to save the shop.'));
      }
    });
  }

  deleteShop(shop: Shop): void {
    const confirmed = window.confirm(
      `Delete "${shop.name}"? Its past rent records stay saved, and the ` +
      'name becomes available again.'
    );

    if (!confirmed) {
      return;
    }

    this.shopService.deleteShop(shop.id).subscribe({
      next: () => {
        this.message.set('Shop deleted.');
        this.loadShops();
      },
      error: error => {
        this.showPopup(apiErrorMessage(error, 'Unable to delete the shop.'));
      }
    });
  }

  // ---------------------------------------------------------------
  // Tenant form
  // ---------------------------------------------------------------

  showAddTenant = signal(false);
  showEditTenant = signal(false);

  addTenantShopId: number | null = null;
  editingTenantId: number | null = null;
  editingTenantShopId: number | null = null;

  tenantName = '';
  mobileNumber = '';
  panCard = '';
  monthlyRent: number | null = null;
  rentDueDay = 5;
  securityDeposit: number | null = null;
  firstRentMonth: 'Current' | 'Next' = 'Current';

  tenantFormError = signal('');
  savingTenant = signal(false);
  firstRentOptions = signal<FirstRentOptions | null>(null);

  openAddTenant(shop: Shop): void {
    this.resetTenantForm();

    this.addTenantShopId = shop.id;
    this.showAddTenant.set(true);
    this.showEditTenant.set(false);
    this.message.set('');

    this.refreshFirstRentOptions();
  }

  // Reloads the two month choices whenever the due day changes, so the
  // landlord sees the real first due date before saving.
  refreshFirstRentOptions(): void {
    const day = Number(this.rentDueDay);

    if (!Number.isFinite(day) || day < 1 || day > 31) {
      return;
    }

    this.tenantService.getFirstRentOptions(day).subscribe({
      next: options => this.firstRentOptions.set(options),
      error: () => this.firstRentOptions.set(null)
    });
  }

  cancelAddTenant(): void {
    this.showAddTenant.set(false);
    this.addTenantShopId = null;
    this.resetTenantForm();
  }

  addTenant(): void {
    this.tenantFormError.set('');

    const validationError = this.validateTenantForm();

    if (validationError) {
      this.tenantFormError.set(validationError);
      return;
    }

    if (!this.addTenantShopId) {
      this.tenantFormError.set('Shop is required.');
      return;
    }

    this.savingTenant.set(true);

    this.tenantService.createTenant({
      name: this.tenantName.trim(),
      mobileNumber: this.mobileNumber.trim(),
      panCard: this.panCard.trim() || undefined,
      monthlyRent: Number(this.monthlyRent),
      rentDueDay: Number(this.rentDueDay),
      securityDeposit: this.securityDeposit ?? undefined,
      shopId: this.addTenantShopId,
      firstRentMonth: this.firstRentMonth
    }).subscribe({
      next: () => {
        this.savingTenant.set(false);
        this.message.set('Tenant added. First rent has been set up.');
        this.cancelAddTenant();
        this.loadShops();
      },
      error: error => {
        this.savingTenant.set(false);
        this.tenantFormError.set(apiErrorMessage(error, 'Unable to add the tenant.'));
      }
    });
  }

  openEditTenant(shop: Shop): void {
    if (!shop.tenant) {
      this.showPopup('No tenant is assigned to this shop.');
      return;
    }

    this.resetTenantForm();

    const tenant = shop.tenant;

    this.editingTenantId = tenant.id;
    this.editingTenantShopId = shop.id;

    this.tenantName = tenant.name;
    this.mobileNumber = tenant.mobileNumber;
    this.panCard = tenant.panCard ?? '';
    this.monthlyRent = tenant.monthlyRent;
    this.rentDueDay = tenant.rentDueDay;
    this.securityDeposit = tenant.securityDeposit ?? null;

    this.showEditTenant.set(true);
    this.showAddTenant.set(false);
    this.message.set('');
  }

  cancelEditTenant(): void {
    this.showEditTenant.set(false);
    this.editingTenantId = null;
    this.editingTenantShopId = null;
    this.resetTenantForm();
  }

  updateTenant(): void {
    this.tenantFormError.set('');

    if (this.editingTenantId === null || !this.editingTenantShopId) {
      this.tenantFormError.set('Tenant not selected.');
      return;
    }

    const validationError = this.validateTenantForm();

    if (validationError) {
      this.tenantFormError.set(validationError);
      return;
    }

    const tenantId = this.editingTenantId;

    this.savingTenant.set(true);

    this.tenantService.updateTenant(tenantId, {
      name: this.tenantName.trim(),
      mobileNumber: this.mobileNumber.trim(),
      panCard: this.panCard.trim() || undefined,
      monthlyRent: Number(this.monthlyRent),
      rentDueDay: Number(this.rentDueDay),
      securityDeposit: this.securityDeposit ?? undefined,
      shopId: this.editingTenantShopId
    }).subscribe({
      next: response => {
        this.savingTenant.set(false);

        const notes: string[] = ['Tenant updated.'];

        if (response.rentChangeEffectiveFrom) {
          notes.push(
            `New rent of Rs ${this.money(response.monthlyRent)} applies from ` +
            `${response.rentChangeEffectiveFrom}.`
          );
        }

        if (response.dueDayAppliedToCurrentMonth) {
          notes.push('New due day applies to this month.');
        }

        this.message.set(notes.join(' '));

        this.cancelEditTenant();
        this.loadShops();

        if (this.selectedTenantId() === tenantId) {
          this.loadLedger(tenantId);
        }
      },
      error: error => {
        this.savingTenant.set(false);
        this.tenantFormError.set(apiErrorMessage(error, 'Unable to update the tenant.'));
      }
    });
  }

  removeTenant(shop: Shop): void {
    const tenant = shop.tenant;

    if (!tenant) {
      this.showPopup('No tenant is assigned to this shop.');
      return;
    }

    const payable = shop.rent?.totalPayable ?? 0;

    if (payable > 0) {
      this.showPopup(
        `${tenant.name} cannot be removed yet. Unpaid dues of ` +
        `Rs ${this.money(payable)} are still pending. Please clear the dues ` +
        'first, all rent history will be preserved.'
      );
      return;
    }

    const confirmed = window.confirm(
      `Remove ${tenant.name} from ${shop.name}? Past rent and payment ` +
      'records will be kept.'
    );

    if (!confirmed) {
      return;
    }

    this.tenantService.deleteTenant(tenant.id).subscribe({
      next: () => {
        this.message.set(`${tenant.name} was removed.`);

        if (this.selectedTenantId() === tenant.id) {
          this.closeLedger();
        }

        this.loadShops();
      },
      error: error => {
        this.showPopup(apiErrorMessage(error, 'Unable to remove the tenant.'));
      }
    });
  }

  private validateTenantForm(): string | null {
    if (!this.tenantName.trim()) {
      return 'Tenant name is required.';
    }

    if (!this.mobileNumber.trim()) {
      return 'Mobile number is required.';
    }

    const rent = Number(this.monthlyRent);

    if (!Number.isFinite(rent) || rent <= 0) {
      return 'Monthly rent must be greater than zero.';
    }

    const day = Number(this.rentDueDay);

    if (!Number.isFinite(day) || day < 1 || day > 31) {
      return 'Rent due day must be between 1 and 31.';
    }

    return null;
  }

  private resetTenantForm(): void {
    this.tenantName = '';
    this.mobileNumber = '';
    this.panCard = '';
    this.monthlyRent = null;
    this.rentDueDay = 5;
    this.securityDeposit = null;
    this.firstRentMonth = 'Current';
    this.tenantFormError.set('');
    this.firstRentOptions.set(null);
  }

  // ---------------------------------------------------------------
  // Manage rent
  // ---------------------------------------------------------------

  paymentType: 'full' | 'partial' = 'full';
  paymentAmount: number | null = null;
  paymentDate = '';
  paymentMode = 'Cash';
  paymentError = signal('');
  savingPayment = signal(false);

  manageRent(tenantId: number): void {
    this.selectedTenantId.set(tenantId);
    this.ledger.set(null);
    this.paymentError.set('');
    this.message.set('');

    this.loadLedger(tenantId);
  }

  loadLedger(tenantId: number): void {
    this.rentService.getLedger(tenantId).subscribe({
      next: ledger => {
        this.ledger.set(ledger);

        this.paymentType = 'full';
        this.paymentAmount = ledger.totalPayable;
        this.paymentDate = ledger.today.slice(0, 10);
        this.paymentMode = 'Cash';
      },
      error: error => {
        this.showPopup(apiErrorMessage(error, 'Unable to load rent details.'));
      }
    });
  }

  closeLedger(): void {
    this.selectedTenantId.set(null);
    this.ledger.set(null);
    this.cancelEditPayment();
    this.paymentError.set('');
  }

  selectFullPayment(): void {
    this.paymentType = 'full';
    this.paymentAmount = this.ledger()?.totalPayable ?? 0;
    this.paymentError.set('');
  }

  selectPartialPayment(): void {
    this.paymentType = 'partial';
    this.paymentAmount = null;
    this.paymentError.set('');
  }

  get remainingAfterPayment(): number {
    const total = this.ledger()?.totalPayable ?? 0;
    const amount = Number(this.paymentAmount) || 0;

    return Math.max(0, total - amount);
  }

  recordPayment(): void {
    const ledger = this.ledger();

    if (!ledger) {
      return;
    }

    this.paymentError.set('');

    const total = ledger.totalPayable;

    if (total <= 0) {
      this.paymentError.set('There is no pending amount right now.');
      return;
    }

    const amount = this.paymentType === 'full' ? total : Number(this.paymentAmount);

    if (!Number.isFinite(amount) || amount <= 0) {
      this.paymentError.set('Enter a valid amount.');
      return;
    }

    if (amount > total) {
      this.paymentError.set(`Amount cannot be more than Rs ${this.money(total)}.`);
      return;
    }

    this.savingPayment.set(true);

    this.paymentService.recordPayment({
      tenantId: ledger.tenant.id,
      amount,
      paymentDate: this.paymentDate || undefined,
      paymentMode: this.paymentMode || 'Cash'
    }).subscribe({
      next: () => {
        this.savingPayment.set(false);
        this.message.set(`Payment of Rs ${this.money(amount)} recorded.`);

        this.loadLedger(ledger.tenant.id);
        this.loadShops();
      },
      error: error => {
        this.savingPayment.set(false);
        this.paymentError.set(apiErrorMessage(error, 'Unable to record the payment.'));
      }
    });
  }

  // ---------------------------------------------------------------
  // Payment correction
  // ---------------------------------------------------------------

  editingPaymentId = signal<number | null>(null);
  editingPaymentAmount: number | null = null;
  editingPaymentDate = '';
  editingPaymentMode = 'Cash';
  editingPaymentNote = '';
  editPaymentError = signal('');

  openEditPayment(payment: Payment): void {
    this.editingPaymentId.set(payment.id);
    this.editingPaymentAmount = payment.amount;
    this.editingPaymentDate = payment.paymentDate.slice(0, 10);
    this.editingPaymentMode = payment.paymentMode || 'Cash';
    this.editingPaymentNote = payment.note ?? '';
    this.editPaymentError.set('');
  }

  cancelEditPayment(): void {
    this.editingPaymentId.set(null);
    this.editingPaymentAmount = null;
    this.editingPaymentDate = '';
    this.editingPaymentMode = 'Cash';
    this.editingPaymentNote = '';
    this.editPaymentError.set('');
  }

  savePaymentCorrection(): void {
    const paymentId = this.editingPaymentId();
    const tenantId = this.selectedTenantId();

    if (paymentId === null || tenantId === null) {
      return;
    }

    const amount = Number(this.editingPaymentAmount);

    if (!Number.isFinite(amount) || amount <= 0) {
      this.editPaymentError.set('Enter a valid amount.');
      return;
    }

    this.paymentService.updatePayment(paymentId, {
      amount,
      paymentDate: this.editingPaymentDate || undefined,
      paymentMode: this.editingPaymentMode,
      note: this.editingPaymentNote.trim() || null
    }).subscribe({
      next: () => {
        this.message.set('Payment corrected. Balance recalculated.');
        this.cancelEditPayment();

        this.loadLedger(tenantId);
        this.loadShops();
      },
      error: error => {
        this.editPaymentError.set(
          apiErrorMessage(error, 'Unable to correct the payment.')
        );
      }
    });
  }

  deletePayment(payment: Payment): void {
    const tenantId = this.selectedTenantId();

    if (tenantId === null) {
      return;
    }

    const confirmed = window.confirm(
      `Delete the payment of Rs ${this.money(payment.amount)}? The rent ` +
      'balance will be recalculated.'
    );

    if (!confirmed) {
      return;
    }

    this.paymentService.deletePayment(payment.id).subscribe({
      next: () => {
        this.message.set('Payment deleted. Balance recalculated.');

        this.loadLedger(tenantId);
        this.loadShops();
      },
      error: error => {
        this.showPopup(apiErrorMessage(error, 'Unable to delete the payment.'));
      }
    });
  }

  // ---------------------------------------------------------------
  // WhatsApp
  // ---------------------------------------------------------------

  sendWhatsAppMessage(): void {
    const ledger = this.ledger();

    if (!ledger) {
      return;
    }

    const digits = ledger.tenant.mobileNumber.replace(/\D/g, '');
    const number = digits.length === 10 ? `91${digits}` : digits;

    if (!number) {
      this.showPopup('This tenant has no mobile number saved.');
      return;
    }

    const text = ledger.totalPayable > 0
      ? `Rent update: Rs ${this.money(ledger.totalPayable)} is currently ` +
        `payable. ${ledger.currentMonth?.timing ?? ''}`.trim()
      : 'Rent update: all dues are cleared. Thank you.';

    window.open(
      `https://wa.me/${number}?text=${encodeURIComponent(text)}`,
      '_blank',
      'noopener,noreferrer'
    );
  }

  // ---------------------------------------------------------------
  // Popup
  // ---------------------------------------------------------------

  showPopup(message: string): void {
    this.popupTitle.set('Action could not be completed');
    this.popupMessage.set(message);
  }

  closePopup(): void {
    this.popupMessage.set('');
  }

  private closeAllModals(): void {
    this.showShopForm.set(false);
    this.showAddTenant.set(false);
    this.showEditTenant.set(false);
    this.popupMessage.set('');
    this.message.set('');
    this.cancelEditPayment();
  }
}
