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

    const scopeKey = await this.resolveScopeKey(scope);
    return Promise.all(rooms.map(room => this.decryptRoomWithKey(room, scopeKey)));
  }

  async decryptRoom(room: ChatRoomListItem, scope: ChatCryptoScope): Promise<ChatRoomListItem> {
    if (!this.cryptoSession.isUnlocked()) {
      return {
        ...room,
        name: room.hasEncryptedContent ? '[Unlock encryption to view]' : room.name
      };
    }

    const scopeKey = await this.resolveScopeKey(scope);
    return this.decryptRoomWithKey(room, scopeKey);
  }

  async encryptRoomName(scope: ChatCryptoScope, name: string): Promise<{ nonce: string; ciphertext: string }> {
    const scopeKey = await this.resolveScopeKey(scope);
    return this.cryptoService.encryptJson<ChatRoomNamePayload>(scopeKey, { name });
  }

  async decryptMessages(messages: ChatMessage[], scope: ChatCryptoScope): Promise<ChatMessage[]> {
    return Promise.all(messages.map(message => this.decryptSingleMessage(message, scope)));
  }

  async decryptSingleMessage(message: ChatMessage, scope: ChatCryptoScope): Promise<ChatMessage> {
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

    const scopeKey = await this.resolveScopeKey(scope);
    return this.decryptMessage(message, scopeKey, scope);
  }

  async encryptMessagePayload(
    scope: ChatCryptoScope,
    body: string,
    authorDisplayName: string,
    newAttachments: PendingAttachment[] = [],
    existingAttachments: ProposalAttachment[] = []
  ): Promise<{ nonce: string; ciphertext: string }> {
    if (scope.fleetId) {
      const fleetKey = await this.cryptoSession.ensureFleetKeyReady(scope.fleetId);
      return this.cryptoService.encryptJson(fleetKey, {
        body,
        authorDisplayName,
        attachments: existingAttachments
      });
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
    scope: ChatCryptoScope
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
      const resolvedAttachments = scope.crewId
        ? await this.proposalCrypto.decryptAttachments(scope.crewId, attachments)
        : [];
      return {
        ...message,
        body: payload.body,
        authorUsername: payload.authorDisplayName ?? message.authorUsername,
        resolvedAttachments
      };
    } catch {
      return { ...message, body: '[Unable to decrypt]' };
    }
  }
}
