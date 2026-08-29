// Status comes from the server, never calculated in the browser.
export type RentStatus = 'Upcoming' | 'Pending' | 'Outstanding' | 'Paid';

export interface Payment {
  id: number;
  rentId: number;
  // Plain calendar date, e.g. "2026-08-27". No time, no timezone.
  paymentDate: string;
  amount: number;
  paymentMode: string;
  note: string | null;
}

export interface RentMonth {
  id: number;
  year: number;
  month: number;
  monthLabel: string;
  amountDue: number;
  amountPaid: number;
  remaining: number;
  dueDate: string;
  isSettled: boolean;
  status: RentStatus;
  timing: string;
  daysUntilDue: number;
  isDueSoon: boolean;
  // Whether this month can be collected right now.
  isPayable: boolean;
  payments: Payment[];
}

export interface CurrentMonth {
  rentId: number;
  year: number;
  month: number;
  monthLabel: string;
  amountDue: number;
  amountPaid: number;
  remaining: number;
  dueDate: string;
  status: RentStatus;
  timing: string;
  daysUntilDue: number;
  isDueSoon: boolean;
  isPayable: boolean;
}

// The next rent that exists but is not collectable yet.
export interface NextExpected {
  monthLabel: string;
  amountDue: number;
  dueDate: string;
  timing: string;
}

export interface TenantLedger {
  tenant: {
    id: number;
    name: string;
    mobileNumber: string;
    monthlyRent: number;
    rentDueDay: number;
    shopName: string | null;
  };
  today: string;
  currentMonth: CurrentMonth | null;
  previousOutstanding: number;
  // The single "Total to be Paid" figure.
  totalPayable: number;
  nextExpected: NextExpected | null;
  upcomingRentAmount: { amount: number; effectiveFrom: string } | null;
  history: RentMonth[];
}
