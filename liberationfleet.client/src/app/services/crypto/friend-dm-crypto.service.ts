import { Injectable, inject } from '@angular/core';
import { DirectMessage } from '../../models/friend.model';
import { ChatMessage } from '../../models/chat.model';
import { PendingAttachment, ProposalAttachment, ProposalComment } from '../../models/proposal.model';
import { ChatCryptoService } from './chat-crypto.service';
import { ProposalCryptoService } from './proposal-crypto.service';
import { CryptoSessionService } from './crypto-session.service';

/**
 * Friend DMs use ECDH (identity keys) — not the crew content key.
 * Legacy messages stamped with a crewId on the envelope still decrypt with that crew key.
 */
@Injectable({
  providedIn: 'root'
})
export class FriendDmCryptoService {
  private chatCrypto = inject(ChatCryptoService);
  private proposalCrypto = inject(ProposalCryptoService);
  private cryptoSession = inject(CryptoSessionService);

  async encryptMessagePayload(
    friendUserId: number,
    body: string,
    authorDisplayName: string,
    newAttachments: PendingAttachment[] = [],
    existingAttachments: ProposalAttachment[] = []
  ): Promise<{ nonce: string; ciphertext: string; keyVersion: number }> {
    await this.cryptoSession.ensureFriendDmKeyReady(friendUserId);
    const encrypted = await this.proposalCrypto.encryptCommentPayload(
      { friendUserId },
      { body, authorDisplayName },
      newAttachments,
      existingAttachments
    );
    return { ...encrypted, keyVersion: 1 };
  }

  async decryptMessages(messages: DirectMessage[], friendUserId: number): Promise<DirectMessage[]> {
    return Promise.all(messages.map(message => this.decryptSingleMessage(message, friendUserId)));
  }

  async decryptSingleMessage(message: DirectMessage, friendUserId: number): Promise<DirectMessage> {
    if (!message.hasEncryptedContent || !message.encryptedPayload) {
      return message;
    }

    const legacyCrewId = message.encryptedPayload.crewId;
    if (legacyCrewId != null && legacyCrewId > 0) {
      const decrypted = await this.chatCrypto.decryptSingleMessage(
        message as ChatMessage,
        { crewId: legacyCrewId },
        { resolveAttachments: false }
      );
      return decrypted as DirectMessage;
    }

    if (!this.cryptoSession.isUnlocked()) {
      return {
        ...message,
        body: '[Unlock encryption to view]',
        authorUsername: message.authorUsername || '[Encrypted]'
      };
    }

    const asComment: ProposalComment = {
      id: message.id,
      authorUserId: message.authorUserId,
      authorUsername: message.authorUsername,
      authorAvatarResourceId: message.authorAvatarResourceId,
      createdAt: message.createdAt as unknown as Date,
      replyCount: 0,
      hasEncryptedContent: message.hasEncryptedContent,
      encryptedPayload: message.encryptedPayload,
      body: message.body ?? '',
      resolvedAttachments: message.resolvedAttachments
    };

    try {
      const [decrypted] = await this.proposalCrypto.decryptComments(
        [asComment],
        { friendUserId }
      );
      return {
        ...message,
        body: decrypted.body,
        authorUsername: decrypted.authorUsername || message.authorUsername,
        resolvedAttachments: decrypted.resolvedAttachments ?? message.resolvedAttachments
      };
    } catch {
      return { ...message, body: '[Unable to decrypt]' };
    }
  }

  async resolveMessageAttachments(messages: DirectMessage[], friendUserId: number): Promise<DirectMessage[]> {
    return Promise.all(
      messages.map(async message => {
        const attachments = message.resolvedAttachments;
        if (!attachments?.length) {
          return message;
        }
        if (attachments.every(attachment => !!attachment.dataUrl || attachment.type === 'file')) {
          return message;
        }

        const legacyCrewId = message.encryptedPayload?.crewId;
        try {
          if (legacyCrewId != null && legacyCrewId > 0) {
            const withCrew = await this.chatCrypto.resolveMessageAttachments(
              [message as ChatMessage],
              { crewId: legacyCrewId }
            );
            return withCrew[0] as DirectMessage;
          }

          const decryptedAttachments = await this.proposalCrypto.decryptAttachments(
            { friendUserId },
            attachments
          );
          return { ...message, resolvedAttachments: decryptedAttachments };
        } catch {
          return message;
        }
      })
    );
  }
}
