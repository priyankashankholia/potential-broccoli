export interface Tenant {
  id: number;
  name: string;
  mobileNumber: string;
  panCard?: string;
  monthlyRent: number;
  rentDueDay: number;
  securityDeposit?: number;
  leaseStartDate?: string;
  leaseEndDate?: string;
  shopId: number;
}
