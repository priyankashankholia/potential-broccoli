import { Tenant } from './tenant';

export interface Shop {
  id: number;
  name: string;
  isOccupied: boolean;
  tenant?: Tenant | null;
}