export interface Tenant {
  id: number;
  name: string;
  mobileNumber: string;
  panCard?: string | null;
  monthlyRent: number;
  rentDueDay: number;
  securityDeposit?: number | null;
}

export interface CreateTenantRequest {
  name: string;
  mobileNumber: string;
  panCard?: string;
  monthlyRent: number;
  rentDueDay: number;
  securityDeposit?: number;
  shopId: number;
  // Relative to the first month whose due date has not passed yet.
  firstRentMonth: 'Current' | 'Next';
}

export interface UpdateTenantRequest {
  name: string;
  mobileNumber: string;
  panCard?: string;
  monthlyRent: number;
  rentDueDay: number;
  securityDeposit?: number;
  shopId: number;
}

export interface UpdateTenantResponse {
  id: number;
  name: string;
  monthlyRent: number;
  rentDueDay: number;
  // Set when the rent amount changed, e.g. "September 2026".
  rentChangeEffectiveFrom: string | null;
  dueDayAppliedToCurrentMonth: boolean;
}

export interface FirstRentOption {
  year: number;
  month: number;
  label: string;
  dueDate: string;

  // True when this month's normal due day has already gone by, so the
  // due date has been pulled forward to today. Only ever set on `current`.
  isBackdated?: boolean;
}

export interface FirstRentOptions {
  current: FirstRentOption;
  next: FirstRentOption;
}
