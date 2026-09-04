import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { GiftLogEncryptedPayload } from '../../models/crypto.model';
import {
  GiftDisplayFlag,
  GiftEntryStatus,
  GiftLogEntry,
  GiftLogType
} from '../../models/gift.model';
import { CryptoApiService } from './crypto-api.service';
import { CryptoService } from './crypto.service';
import { CryptoSessionService } from './crypto-session.service';

@Injectable({
  providedIn: 'root'
})
export class GiftLogCryptoService {
  static readonly crewGiftRecipientName = 'the crew';

  constructor(
    private cryptoService: CryptoService,
    private cryptoApi: CryptoApiService,
    private cryptoSession: CryptoSessionService
  ) {}

  async decryptEntries(entries: GiftLogEntry[], crewId: number): Promise<GiftLogEntry[]> {
    if (!this.cryptoSession.isUnlocked()) {
      return entries.map(entry => this.maskEncryptedEntry(entry));
    }

    try {
      // Warm multi-version key cache once for the list.
      await this.cryptoSession.warmCrewKeys(crewId);
    } catch {
      return entries.map(entry => ({
        ...entry,
        message: entry.hasEncryptedContent ? '[Unable to decrypt gift entry]' : entry.message
      }));
    }

    const decrypted = await Promise.all(entries.map(async entry => {
      if (!entry.hasEncryptedContent || !entry.encryptedPayload) {
        return entry;
      }

      try {
        const payload = await this.cryptoSession.decryptWithCrewKeyFallback(
          crewId,
          entry.encryptedPayload.keyVersion,
          key => this.cryptoService.decryptJson<GiftLogEncryptedPayload>(
            key,
            entry.encryptedPayload!.nonce,
            entry.encryptedPayload!.ciphertext
          )
        );
        const giverName = payload.giverName;
        const recipientName = payload.recipientName;
        const middlemanName = payload.middlemanName ?? undefined;
        const platform = payload.platform;
        const isLibraryOfThings = (platform || entry.platform) === 'Library of Things';
        const rebuilt = this.buildDisplayMessage(
          entry.type,
          giverName || entry.giverName,
          recipientName || entry.recipientName,
          middlemanName ?? entry.middlemanName,
          entry.amount,
          platform || entry.platform,
          entry.status,
          entry.displayFlag
        );
        // Prefer the stored encrypted body (historical freeform posts / LoT / celebrations).
        // Fall back to a rebuilt template when the payload has no message text.
        const storedMessage = (payload.message || '').trim();
        const message = storedMessage
          ? storedMessage
          : (this.isCelebrationType(entry.type) ? (entry.message || rebuilt) : rebuilt);
        return {
          ...entry,
          giverName,
          recipientName,
          middlemanName,
          platform,
          message
        };
      } catch {
        return {
          ...entry,
          message: '[Unable to decrypt gift entry]'
        };
      }
    }));

    return decrypted;
  }

  async encryptAndStoreEntry(entry: GiftLogEntry, crewId: number, keyVersion?: number): Promise<void> {
    if (!this.cryptoSession.isUnlocked()) {
      return;
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const resolvedKeyVersion = keyVersion
      ?? this.cryptoSession.getCrewKeyVersion(crewId)
      ?? 1;
    const payload: GiftLogEncryptedPayload = {
      message: entry.message,
      giverName: entry.giverName,
      recipientName: entry.recipientName,
      middlemanName: entry.middlemanName ?? null,
      platform: entry.platform
    };
    const encrypted = await this.cryptoService.encryptJson(crewKey, payload);
    await firstValueFrom(this.cryptoApi.upsertEncryptedContent({
      contentType: 'GiftLogEntry',
      resourceId: entry.id.toString(),
      crewId,
      keyVersion: resolvedKeyVersion,
      nonce: encrypted.nonce,
      ciphertext: encrypted.ciphertext
    }));
  }

  async encryptLibraryCreatorContribution(
    gift: {
      giftId: number;
      contributorUserId: number;
      contributorUsername: string;
      amount: number;
      itemTitle: string;
      recipientUserId: number;
      recipientUsername: string;
      crewGiftRecipientUserId: number;
    },
    crewId: number
  ): Promise<void> {
    const message = this.buildLibraryOfThingsMessage(
      gift.contributorUsername,
      gift.amount,
      gift.itemTitle,
      GiftLogCryptoService.crewGiftRecipientName
    );
    await this.encryptAndStoreEntry({
      id: gift.giftId,
      type: 'direct',
      giverId: gift.contributorUserId,
      giverName: gift.contributorUsername,
      recipientId: gift.crewGiftRecipientUserId,
      recipientName: GiftLogCryptoService.crewGiftRecipientName,
      amount: gift.amount,
      platform: 'Library of Things',
      timestamp: new Date(),
      message,
      relatedUserIds: [gift.contributorUserId, gift.recipientUserId, gift.crewGiftRecipientUserId],
      hasEncryptedContent: false
    }, crewId);
  }

  async encryptLibraryCompleterContribution(
    gift: {
      giftId: number;
      contributorUserId: number;
      contributorUsername: string;
      amount: number;
      itemTitle: string;
      recipientUserId: number;
      recipientUsername: string;
      crewGiftRecipientUserId: number;
    },
    crewId: number
  ): Promise<void> {
    const message = this.buildLibraryOfThingsMessage(
      gift.contributorUsername,
      gift.amount,
      gift.itemTitle,
      gift.recipientUsername
    );
    await this.encryptAndStoreEntry({
      id: gift.giftId,
      type: 'direct',
      giverId: gift.contributorUserId,
      giverName: gift.contributorUsername,
      recipientId: gift.recipientUserId,
      recipientName: gift.recipientUsername,
      amount: gift.amount,
      platform: 'Library of Things',
      timestamp: new Date(),
      message,
      relatedUserIds: [gift.contributorUserId, gift.recipientUserId],
      hasEncryptedContent: false
    }, crewId);
  }

  async encryptLibraryReceptionGift(
    gift: {
      giftId: number;
      contributorUserId: number;
      contributorUsername: string;
      amount: number;
      itemTitle: string;
      recipientUserId: number;
      recipientUsername: string;
    },
    crewId: number
  ): Promise<void> {
    const message = this.buildLibraryOfThingsMessage(
      gift.contributorUsername,
      gift.amount,
      gift.itemTitle,
      gift.recipientUsername
    );
    await this.encryptAndStoreEntry({
      id: gift.giftId,
      type: 'direct',
      giverId: gift.contributorUserId,
      giverName: gift.contributorUsername,
      recipientId: gift.recipientUserId,
      recipientName: gift.recipientUsername,
      amount: gift.amount,
      platform: 'Library of Things',
      timestamp: new Date(),
      message,
      relatedUserIds: [gift.contributorUserId, gift.recipientUserId],
      hasEncryptedContent: false
    }, crewId);
  }

  async encryptLibraryTaskCompletion(
    gift: {
      giftId: number;
      contributorUserId: number;
      contributorUsername: string;
      amount: number;
      itemTitle: string;
      recipientUserId: number;
      recipientUsername: string;
      crewGiftRecipientUserId: number;
    },
    crewId: number
  ): Promise<void> {
    const amountText = Number.isInteger(gift.amount)
      ? gift.amount.toString()
      : gift.amount.toFixed(2).replace(/\.?0+$/, '');
    const title = gift.itemTitle.trim() || 'a quest';
    const message = `${gift.contributorUsername} gifted ${gift.recipientUsername} $${amountText} worth of ${title}`;
    await this.encryptAndStoreEntry({
      id: gift.giftId,
      type: 'direct',
      giverId: gift.contributorUserId,
      giverName: gift.contributorUsername,
      recipientId: gift.recipientUserId,
      recipientName: gift.recipientUsername,
      amount: gift.amount,
      platform: 'Library of Things',
      timestamp: new Date(),
      message,
      relatedUserIds: [gift.contributorUserId, gift.recipientUserId, gift.crewGiftRecipientUserId],
      hasEncryptedContent: false
    }, crewId);
  }

  buildLibraryOfThingsMessage(
    giverName: string,
    amount: number,
    itemTitle: string,
    recipientName: string
  ): string {
    const amountText = Number.isInteger(amount) ? amount.toString() : amount.toFixed(2).replace(/\.?0+$/, '');
    const item = itemTitle.trim() || 'an offering';
    return `${giverName} gave $${amountText} in ${item} to ${recipientName} via the Library of Things`;
  }

  buildDisplayMessage(
    type: GiftLogType,
    giverName: string,
    recipientName: string,
    middlemanName: string | undefined,
    amount: number,
    platform: string,
    status?: GiftEntryStatus,
    displayFlag?: GiftDisplayFlag | null
  ): string {
    const amountText = amount.toString();
    let baseMessage: string;

    switch (type) {
      case 'direct':
        baseMessage = `${giverName} gave $${amountText} to ${recipientName} via ${platform}`;
        break;
      case 'initiated':
        baseMessage = `${giverName} initiated a $${amountText} gift to ${recipientName} through ${middlemanName ?? 'middleman'} via ${platform}`;
        break;
      case 'completed':
        baseMessage = `${middlemanName ?? 'Middleman'} completed ${giverName}'s $${amountText} gift to ${recipientName} via ${platform.toUpperCase()}`;
        break;
      case 'seasonstarted':
        baseMessage = 'A new mutual aid season has begun!';
        break;
      case 'cyclestarted':
        baseMessage = recipientName && recipientName !== '[Encrypted]' && recipientName !== 'Unknown'
          ? `A new reception cycle has started for ${recipientName}!`
          : 'A new reception cycle has started!';
        break;
      case 'survivalthresholdsrefreshed':
        baseMessage = 'Survival thresholds have refreshed for the new month!';
        break;
      default:
        baseMessage = '';
    }

    if (this.isCelebrationType(type)) {
      return baseMessage;
    }

    if (displayFlag === 'notComplete') {
      return `${baseMessage} (Not Complete)`;
    }
    if (displayFlag === 'cantComplete') {
      return `${baseMessage} (Can't Complete)`;
    }
    if (displayFlag === 'unverified' || status === 'unverified') {
      return `${baseMessage} (Unverified)`;
    }
    if (type === 'initiated' && status === 'completed') {
      return `${baseMessage} (Completed)`;
    }
    if (type === 'initiated' && status === 'pending') {
      return `${baseMessage} (Pending)`;
    }
    if (type === 'completed' && status === 'pending') {
      return `${baseMessage} (Awaiting confirmation)`;
    }

    return baseMessage;
  }

  async backfillUnencryptedEntries(
    entries: GiftLogEntry[],
    crewId: number,
    activeUserId: number
  ): Promise<void> {
    if (!this.cryptoSession.isUnlocked() || activeUserId <= 0) {
      return;
    }

    const targets = entries.filter(entry =>
      !entry.hasEncryptedContent
      && entry.giverId === activeUserId
      && !this.isCelebrationType(entry.type));
    for (const entry of targets) {
      try {
        await this.encryptAndStoreEntry(entry, crewId);
      } catch {
        // Backfill is best-effort; do not block gift log display on a single failure.
      }
    }
  }

  isCelebrationType(type: GiftLogType | string | undefined | null): boolean {
    const normalized = (type ?? '').toLowerCase();
    return normalized === 'seasonstarted'
      || normalized === 'cyclestarted'
      || normalized === 'survivalthresholdsrefreshed';
  }

  private maskEncryptedEntry(entry: GiftLogEntry): GiftLogEntry {
    if (!entry.hasEncryptedContent) {
      return entry;
    }

    if (this.isCelebrationType(entry.type)) {
      return {
        ...entry,
        message: this.buildDisplayMessage(
          entry.type,
          entry.giverName,
          entry.recipientName,
          entry.middlemanName,
          entry.amount,
          entry.platform,
          entry.status,
          entry.displayFlag
        )
      };
    }

    return {
      ...entry,
      giverName: '[Encrypted]',
      recipientName: '[Encrypted]',
      middlemanName: undefined,
      platform: '[Encrypted]',
      message: '[Unlock encryption to view this gift]'
    };
  }
}
