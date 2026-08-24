import { Injectable } from '@angular/core';
import { ChatMessage, ChatRoomListItem, ChatRoomNamePayload } from '../../models/chat.model';
import { PendingAttachment, ProposalAttachment, ProposalCommentEncryptedPayload } from '../../models/proposal.model';
import { CryptoSessionService } from './crypto-session.service';
import { CryptoService } from './crypto.service';
import { ProposalCryptoService } from './proposal-crypto.service';

export interface ChatCryptoScope {
  crewId?: number;
  fleetId?: number;
}

@Injectable({
  providedIn: 'root'
})
export class ChatCryptoService {
  constructor(
    private cryptoSession: CryptoSessionService,
    private cryptoService: CryptoService,
    private proposalCrypto: ProposalCryptoService
  ) {}

  async decryptRooms(rooms: ChatRoomListItem[], scope: ChatCryptoScope): Promise<ChatRoomListItem[]> {
    if (!this.cryptoSession.isUnlocked()) {
      return rooms.map(room => ({
        ...room,
        name: room.hasEncryptedContent ? '[Unlock encryption to view]' : room.name
      }));
    }

    let scopeKey: CryptoKey;
    try {
      scopeKey = await this.resolveScopeKey(scope);
    } catch {
      return rooms.map(room => ({
        ...room,
        name: room.hasEncryptedContent ? '[Unable to decrypt]' : room.name
      }));
    }
    return Promise.all(rooms.map(room => this.decryptRoomWithKey(room, scopeKey)));
  }

  async decryptRoom(room: ChatRoomListItem, scope: ChatCryptoScope): Promise<ChatRoomListItem> {
    if (!this.cryptoSession.isUnlocked()) {
      return {
        ...room,
        name: room.hasEncryptedContent ? '[Unlock encryption to view]' : room.name
      };
    }

    let scopeKey: CryptoKey;
    try {
      scopeKey = await this.resolveScopeKey(scope);
    } catch {
      return {
        ...room,
        name: room.hasEncryptedContent ? '[Unable to decrypt]' : room.name
      };
    }
    return this.decryptRoomWithKey(room, scopeKey);
  }

  async encryptRoomName(scope: ChatCryptoScope, name: string): Promise<{ nonce: string; ciphertext: string }> {
    const scopeKey = await this.resolveScopeKey(scope);
    return this.cryptoService.encryptJson<ChatRoomNamePayload>(scopeKey, { name });
  }

  async decryptMessages(messages: ChatMessage[], scope: ChatCryptoScope): Promise<ChatMessage[]> {
    // Text/metadata only — callers should invoke resolveMessageAttachments afterward so
    // media work cannot block the room loading spinner.
    return Promise.all(
      messages.map(message => this.decryptSingleMessage(message, scope, { resolveAttachments: false }))
    );
  }

  /** Resolve attachment media URLs for messages that already have decrypted text. */
  async resolveMessageAttachments(messages: ChatMessage[], scope: ChatCryptoScope): Promise<ChatMessage[]> {
    if (!(scope.crewId || scope.fleetId)) {
      return messages;
    }

    return Promise.all(
      messages.map(async message => {
        const attachments = message.resolvedAttachments;
        if (!attachments?.length) {
          return message;
        }
        // Already has playable/stream URLs.
        if (attachments.every(attachment => !!attachment.dataUrl || attachment.type === 'file')) {
          return message;
        }
        try {
          const resolvedAttachments = await this.proposalCrypto.decryptAttachments(
            scope.crewId ? { crewId: scope.crewId } : { fleetId: scope.fleetId },
            attachments
          );
          return { ...message, resolvedAttachments };
        } catch {
          return message;
        }
      })
    );
  }

  async decryptSingleMessage(
    message: ChatMessage,
    scope: ChatCryptoScope,
    options?: { resolveAttachments?: boolean }
  ): Promise<ChatMessage> {
    if (!message.hasEncryptedContent || !message.encryptedPayload) {
      return message;
    }

    if (!this.cryptoSession.isUnlocked()) {
      return {
        ...message,
        body: '[Unlock encryption to view]',
        authorUsername: message.authorUsername || '[Encrypted]'
      };
    }

    try {
      const scopeKey = await this.resolveScopeKey(scope);
      return await this.decryptMessage(message, scopeKey, scope, options);
    } catch {
      return { ...message, body: '[Unable to decrypt]' };
    }
  }

  async encryptMessagePayload(
    scope: ChatCryptoScope,
    body: string,
    authorDisplayName: string,
    newAttachments: PendingAttachment[] = [],
    existingAttachments: ProposalAttachment[] = []
  ): Promise<{ nonce: string; ciphertext: string }> {
    if (scope.fleetId) {
      return this.proposalCrypto.encryptCommentPayload(
        { fleetId: scope.fleetId },
        { body, authorDisplayName },
        newAttachments,
        existingAttachments
      );
    }

    return this.proposalCrypto.encryptCommentPayload(
      scope.crewId!,
      { body, authorDisplayName },
      newAttachments,
      existingAttachments
    );
  }

  private async resolveScopeKey(scope: ChatCryptoScope): Promise<CryptoKey> {
    if (scope.fleetId) {
      return this.cryptoSession.ensureFleetKeyReady(scope.fleetId);
    }

    if (scope.crewId) {
      return this.cryptoSession.ensureCrewKeyReady(scope.crewId);
    }

    throw new Error('Encryption scope is required.');
  }

  private async decryptRoomWithKey(room: ChatRoomListItem, scopeKey: CryptoKey): Promise<ChatRoomListItem> {
    if (!room.hasEncryptedContent || !room.encryptedPayload) {
      return room;
    }

    try {
      const payload = await this.cryptoService.decryptJson<ChatRoomNamePayload>(
        scopeKey,
        room.encryptedPayload.nonce,
        room.encryptedPayload.ciphertext
      );
      return { ...room, name: payload.name };
    } catch {
      return { ...room, name: '[Unable to decrypt]' };
    }
  }

  private async decryptMessage(
    message: ChatMessage,
    scopeKey: CryptoKey,
    scope: ChatCryptoScope,
    options?: { resolveAttachments?: boolean }
  ): Promise<ChatMessage> {
    if (!message.hasEncryptedContent || !message.encryptedPayload) {
      return message;
    }

    try {
      const payload = await this.cryptoService.decryptJson<ProposalCommentEncryptedPayload>(
        scopeKey,
        message.encryptedPayload.nonce,
        message.encryptedPayload.ciphertext
      );
      const attachments = payload.attachments ?? [];
      const resolveAttachments = options?.resolveAttachments !== false;
      const resolvedAttachments = resolveAttachments && (scope.crewId || scope.fleetId)
        ? await this.proposalCrypto.decryptAttachments(
          scope.crewId ? { crewId: scope.crewId } : { fleetId: scope.fleetId },
          attachments
        )
        : attachments;
      return {
        ...message,
        body: payload.body,
        authorUsername: message.isAnonymous
          ? 'Anonymous'
          : (payload.authorDisplayName ?? message.authorUsername),
        authorAvatarResourceId: message.isAnonymous ? null : message.authorAvatarResourceId,
        resolvedAttachments
      };
    } catch {
      return { ...message, body: '[Unable to decrypt]' };
    }
  }
}
