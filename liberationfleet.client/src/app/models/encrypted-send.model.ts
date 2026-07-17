export interface EncryptedContentSendPayload {
  nonce?: string;
  ciphertext?: string;
  keyVersion?: number;
  mentionedUserIds?: number[];
  /** Plaintext preview for notification bodies (truncated to 200 server-side). */
  notificationPreview?: string;
  title?: string;
  description?: string;
  body?: string;
}
