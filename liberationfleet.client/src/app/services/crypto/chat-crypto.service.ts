import { Injectable } from '@angular/core';
import { ChatMessage, ChatRoomListItem, ChatRoomNamePayload } from '../../models/chat.model';
import { PendingAttachment, ProposalAttachment, ProposalCommentEncryptedPayload } from '../../models/proposal.model';
import { CryptoSessionService } from './crypto-session.service';
import { CryptoService } from './crypto.service';
import { ProposalCryptoService } from './proposal-crypto.service';

@Injectable({
  providedIn: 'root'
})
export class ChatCryptoService {
  constructor(
    private cryptoSession: CryptoSessionService,
    private cryptoService: CryptoService,
    private proposalCrypto: ProposalCryptoService
  ) {}

  async decryptRooms(rooms: ChatRoomListItem[], crewId: number): Promise<ChatRoomListItem[]> {
    if (!this.cryptoSession.isUnlocked()) {
      return rooms.map(room => ({
        ...room,
        name: room.hasEncryptedContent ? '[Unlock encryption to view]' : room.name
      }));
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    return Promise.all(rooms.map(room => this.decryptRoomWithKey(room, crewKey)));
  }

  async decryptRoom(room: ChatRoomListItem, crewId: number): Promise<ChatRoomListItem> {
    if (!this.cryptoSession.isUnlocked()) {
      return {
        ...room,
        name: room.hasEncryptedContent ? '[Unlock encryption to view]' : room.name
      };
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    return this.decryptRoomWithKey(room, crewKey);
  }

  async encryptRoomName(crewId: number, name: string): Promise<{ nonce: string; ciphertext: string }> {
    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    return this.cryptoService.encryptJson<ChatRoomNamePayload>(crewKey, { name });
  }

  async decryptMessages(messages: ChatMessage[], crewId: number): Promise<ChatMessage[]> {
    if (!this.cryptoSession.isUnlocked()) {
      return messages.map(message => ({
        ...message,
        body: '[Unlock encryption to view]',
        authorUsername: message.authorUsername || '[Encrypted]'
      }));
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    return Promise.all(messages.map(message => this.decryptMessage(message, crewKey, crewId)));
  }

  async decryptSingleMessage(message: ChatMessage, crewId: number): Promise<ChatMessage> {
    if (!this.cryptoSession.isUnlocked()) {
      return {
        ...message,
        body: '[Unlock encryption to view]',
        authorUsername: message.authorUsername || '[Encrypted]'
      };
    }

    const crewKey = await this.cryptoSession.ensureCrewKeyReady(crewId);
    return this.decryptMessage(message, crewKey, crewId);
  }

  async encryptMessagePayload(
    crewId: number,
    body: string,
    authorDisplayName: string,
    newAttachments: PendingAttachment[] = [],
    existingAttachments: ProposalAttachment[] = []
  ): Promise<{ nonce: string; ciphertext: string }> {
    return this.proposalCrypto.encryptCommentPayload(
      crewId,
      { body, authorDisplayName },
      newAttachments,
      existingAttachments
    );
  }

  private async decryptRoomWithKey(room: ChatRoomListItem, crewKey: CryptoKey): Promise<ChatRoomListItem> {
    if (!room.hasEncryptedContent || !room.encryptedPayload) {
      return room;
    }

    try {
      const payload = await this.cryptoService.decryptJson<ChatRoomNamePayload>(
        crewKey,
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
    crewKey: CryptoKey,
    crewId: number
  ): Promise<ChatMessage> {
    if (!message.hasEncryptedContent || !message.encryptedPayload) {
      return message;
    }

    try {
      const payload = await this.cryptoService.decryptJson<ProposalCommentEncryptedPayload>(
        crewKey,
        message.encryptedPayload.nonce,
        message.encryptedPayload.ciphertext
      );
      const attachments = payload.attachments ?? [];
      const resolvedAttachments = await this.proposalCrypto.decryptAttachments(crewId, attachments);
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
