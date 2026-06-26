import { EncryptedPayload } from './crypto.model';

export interface RuleEncryptedPayload {
  title: string;
  description: string;
  authorDisplayName?: string;
}

export interface RuleListItem {
  id: number;
  createdByUserId: number;
  createdByUsername: string;
  createdAt: string;
  updatedAt: string;
  hasEncryptedContent?: boolean;
  encryptedPayload?: EncryptedPayload | null;
  title?: string;
  description?: string;
  descriptionPreview?: string;
}

export interface RuleDetail extends RuleListItem {}

export interface RuleListResponse {
  success: boolean;
  message: string;
  items: RuleListItem[];
}

export interface RuleDetailResponse {
  success: boolean;
  message: string;
  rule?: RuleDetail;
}

export interface RuleOperationResponse {
  success: boolean;
  message: string;
  ruleId?: number;
  proposalsSubmitted?: boolean;
  proposalId?: number;
}

export interface RuleWritePayload {
  nonce: string;
  ciphertext: string;
  keyVersion?: number;
  plaintextTitle: string;
  plaintextDescription: string;
  plaintextOldTitle?: string;
  plaintextOldDescription?: string;
}

export interface RuleDeletePayload {
  plaintextTitle: string;
  plaintextDescription: string;
}
