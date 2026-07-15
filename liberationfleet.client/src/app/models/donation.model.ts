export interface DonationCampaignPrompt {
  show: boolean;
  messageVariant: 'crew' | 'fleet' | string;
  message: string;
  donationsEnabled: boolean;
}

export interface DonationCheckoutResponse {
  success: boolean;
  message: string;
  checkoutUrl?: string | null;
}

export interface DonationSummary {
  success: boolean;
  message: string;
  currentTaxYear: number;
  previousTaxYear: number;
  currentTaxYearTotalUsd: number;
  previousTaxYearTotalUsd: number;
  donationsEnabled: boolean;
}

export const DONATION_PRESET_AMOUNTS_USD = [5, 10, 25, 50, 100] as const;
