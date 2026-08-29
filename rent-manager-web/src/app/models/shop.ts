import { RentStatus } from './rent';
import { Tenant } from './tenant';

export interface ShopRentSummary {
  rentId: number | null;
  year: number;
  month: number;
  monthLabel: string;
  amountDue: number;
  amountPaid: number;
  dueDate: string | null;
  status: RentStatus;
  timing: string;
  daysUntilDue: number;
  isDueSoon: boolean;
  previousOutstanding: number;
  // This month plus anything carried forward that is collectable now.
  totalPayable: number;
}

export interface Shop {
  id: number;
  name: string;
  isOccupied: boolean;
  tenant?: Tenant | null;
  rent?: ShopRentSummary | null;
}
