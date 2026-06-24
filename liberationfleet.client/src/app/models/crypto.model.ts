export type EncryptedContentType =
  | 'GiftLogEntry'
  | 'DirectMessage'
  | 'ChatRoomMessage'
  | 'ForumPost'
  | 'ProjectForumPost'
  | 'Proposal'
  | 'RulesDocument'
  | 'LibraryItem'
  | 'ImageAsset'
  | 'AudioAsset'
  | 'VideoAsset'
  | 'ProposalComment'
  | 'ForumComment'
  | 'ProjectComment'
  | 'ChatRoomName';

export interface UserKeyBundle {
  userId: number;
  identityPublicKey: string;
  keyVersion: number;
  updatedAt: string;
}

export interface UserPrivateKeyBackup {
  salt: string;
  iv: string;
  ciphertext: string;
  keyVersion: number;
}

export interface CrewKeyDistribution {
  crewId: number;
  userId: number;
  keyVersion: number;
  wrappedCrewKey: string;
  wrapNonce: string;
  wrappedByUserId: number;
}

export interface CrewKeyState {
  latestKeyVersion?: number | null;
  myDistribution?: CrewKeyDistribution | null;
  distributions: CrewKeyDistribution[];
}

export interface EncryptedPayload {
  keyVersion: number;
  nonce: string;
  ciphertext: string;
}

export interface EncryptedContentEnvelope {
  contentType: EncryptedContentType;
  resourceId: string;
  crewId?: number | null;
  authorUserId: number;
  keyVersion: number;
  nonce: string;
  ciphertext: string;
  updatedAt: string;
}

export interface CryptoOperationResponse {
  success: boolean;
  message: string;
}

export interface GiftLogEncryptedPayload {
  message: string;
  giverName: string;
  recipientName: string;
  middlemanName?: string | null;
  platform: string;
}
