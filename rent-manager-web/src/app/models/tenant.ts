export interface Tenant {
  id: number;
  name: string;
  mobileNumber: string;
  panCard?: string | null;
  monthlyRent: number;
  rentDueDay: number;
  securityDeposit?: number | null;
  leaseStartDate?: string | null;
  leaseEndDate?: string | null;
  shopId: number;
}