export interface Shop {
  id: number;
  name: string;
  isOccupied: boolean;
  tenant: {
    id: number;
    name: string;
    mobileNumber: string;
  } | null;
}