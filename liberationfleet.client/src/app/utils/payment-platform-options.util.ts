import { PaymentPlatformOption } from '../models/gift.model';
import { CUSTOM_PLATFORM_OPTION_ID, PaymentPlatformAccount } from '../models/profile.model';

export function mergePaymentPlatformOptions(
  options: PaymentPlatformOption[],
  accounts: PaymentPlatformAccount[]
): PaymentPlatformOption[] {
  const byId = new Map(options.map(option => [option.id, option]));

  for (const account of accounts) {
    if (account.platformId <= 0) {
      continue;
    }

    const name = resolvePlatformName(account);
    if (!name) {
      continue;
    }

    byId.set(account.platformId, { id: account.platformId, name });
  }

  return Array.from(byId.values()).sort((a, b) => a.name.localeCompare(b.name));
}

/**
 * Prefer an existing system platform when adding a new account so users do not
 * invent duplicate custom names for PayPal/Venmo/etc. already in the crew.
 */
export function defaultSystemPlatformId(
  options: PaymentPlatformOption[],
  preferredPlatformId?: number
): number {
  if (
    preferredPlatformId != null
    && preferredPlatformId > 0
    && options.some(option => option.id === preferredPlatformId)
  ) {
    return preferredPlatformId;
  }

  return options.length > 0 ? options[0].id : CUSTOM_PLATFORM_OPTION_ID;
}

export function defaultSystemPlatformSelection(
  options: PaymentPlatformOption[],
  preferredPlatformId?: number
): { platformId: number; platform: string } {
  const platformId = defaultSystemPlatformId(options, preferredPlatformId);
  if (platformId === CUSTOM_PLATFORM_OPTION_ID) {
    return { platformId, platform: '' };
  }

  return {
    platformId,
    platform: options.find(option => option.id === platformId)?.name ?? ''
  };
}

function resolvePlatformName(account: PaymentPlatformAccount): string {
  if (typeof account.platform === 'string' && account.platform.trim()) {
    return account.platform.trim();
  }

  return account.customPlatformName?.trim() ?? '';
}
