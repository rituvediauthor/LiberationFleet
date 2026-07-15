export interface EncryptedContentSendPayload {
  nonce?: string;
  ciphertext?: string;
  keyVersion?: number;
  mentionedUserIds?: number[];
}
