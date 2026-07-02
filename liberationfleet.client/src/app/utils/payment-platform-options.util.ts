import { PaymentPlatformOption } from '../models/gift.model';
import { PaymentPlatformAccount } from '../models/profile.model';

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

function resolvePlatformName(account: PaymentPlatformAccount): string {
  if (typeof account.platform === 'string' && account.platform.trim()) {
    return account.platform.trim();
  }

  return account.customPlatformName?.trim() ?? '';
}
