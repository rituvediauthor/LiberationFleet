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

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    const decrypted = await Promise.all(entries.map(async entry => {
      if (!entry.hasEncryptedContent || !entry.encryptedPayload) {
        return entry;
      }

      try {
        const payload = await this.cryptoService.decryptJson<GiftLogEncryptedPayload>(
          crewKey,
          entry.encryptedPayload.nonce,
          entry.encryptedPayload.ciphertext
        );
        return {
          ...entry,
          giverName: payload.giverName,
          recipientName: payload.recipientName,
          middlemanName: payload.middlemanName ?? undefined,
          platform: payload.platform,
          message: payload.message
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

  async encryptAndStoreEntry(entry: GiftLogEntry, crewId: number, keyVersion = 1): Promise<void> {
    if (!this.cryptoSession.isUnlocked()) {
      return;
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
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
      keyVersion,
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
    const message = `${gift.contributorUsername} contributed $${gift.amount} when ${gift.recipientUsername} acquired "${gift.itemTitle}"`;
    await this.encryptAndStoreEntry({
      id: gift.giftId,
      type: 'direct',
      giverId: gift.contributorUserId,
      giverName: gift.contributorUsername,
      recipientId: gift.crewGiftRecipientUserId,
      recipientName: GiftLogCryptoService.crewGiftRecipientName,
      amount: gift.amount,
      platform: 'In-kind (Library)',
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
    const message = `${gift.contributorUsername} contributed $${gift.amount} of value by helping ${gift.recipientUsername} acquire ${gift.itemTitle}`;
    await this.encryptAndStoreEntry({
      id: gift.giftId,
      type: 'direct',
      giverId: gift.contributorUserId,
      giverName: gift.contributorUsername,
      recipientId: gift.crewGiftRecipientUserId,
      recipientName: GiftLogCryptoService.crewGiftRecipientName,
      amount: gift.amount,
      platform: 'In-kind (Library)',
      timestamp: new Date(),
      message,
      relatedUserIds: [gift.contributorUserId, gift.recipientUserId, gift.crewGiftRecipientUserId],
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
    const message = `${gift.contributorUsername} provided $${gift.amount} of ${gift.itemTitle} to ${gift.recipientUsername}`;
    await this.encryptAndStoreEntry({
      id: gift.giftId,
      type: 'direct',
      giverId: gift.contributorUserId,
      giverName: gift.contributorUsername,
      recipientId: gift.recipientUserId,
      recipientName: gift.recipientUsername,
      amount: gift.amount,
      platform: 'In-kind (Library)',
      timestamp: new Date(),
      message,
      relatedUserIds: [gift.contributorUserId, gift.recipientUserId],
      hasEncryptedContent: false
    }, crewId);
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
      default:
        baseMessage = '';
    }

    if (displayFlag === 'notComplete') {
      return `${baseMessage} (Not Complete)`;
    }
    if (displayFlag === 'cantComplete') {
      return `${baseMessage} (Can't Complete)`;
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

    const targets = entries.filter(entry => !entry.hasEncryptedContent && entry.giverId === activeUserId);
    for (const entry of targets) {
      try {
        await this.encryptAndStoreEntry(entry, crewId);
      } catch {
        // Backfill is best-effort; do not block gift log display on a single failure.
      }
    }
  }

  private maskEncryptedEntry(entry: GiftLogEntry): GiftLogEntry {
    if (!entry.hasEncryptedContent) {
      return entry;
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
