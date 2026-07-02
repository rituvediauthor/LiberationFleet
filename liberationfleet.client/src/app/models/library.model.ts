export interface LibraryCategory {
  id: number;
  name: string;
}

export interface LibraryUnitListItem {
  unitId: number;
  offeringId: number;
  holderUserId: number;
  holderUsername: string;
  title: string;
  descriptionPreview: string;
  categories: string[];
  thumbnailResourceId?: string | null;
  thumbnailUrl?: string | null;
  hasEncryptedContent?: boolean;
  remainingStock?: number | null;
  quantityNotApplicable?: boolean;
  isOutOfStock?: boolean;
  offeringKind?: string;
  fulfillmentMode?: string;
}

export interface LibraryCategoryListResponse {
  success: boolean;
  message: string;
  items: LibraryCategory[];
}

export interface LibraryUnitListResponse {
  success: boolean;
  message: string;
  items: LibraryUnitListItem[];
  hasMore: boolean;
}

export interface LibraryUnitListPage {
  items: LibraryUnitListItem[];
  hasMore: boolean;
}

export interface CreateLibraryOfferingRequest {
  title: string;
  descriptionPreview: string;
  categoryIds: number[];
  valuePerUnit: number;
  unitLabel?: string | null;
  quantity: number;
  quantityNotApplicable?: boolean;
  thumbnailResourceId?: string | null;
  kind?: string;
  fulfillmentMode?: string;
  nonce: string;
  ciphertext: string;
  keyVersion?: number;
}

export interface LibraryOfferingOperationResponse {
  success: boolean;
  message: string;
  offeringId?: number;
  giftId?: number;
  unitIds?: number[];
}

export interface LibraryUnitViewerContext {
  isHolder: boolean;
  canRequest: boolean;
  canRecordAcquisition?: boolean;
  maxRequestQuantity?: number;
  brokenPendingConfirmation?: boolean;
  isRetired?: boolean;
  canReportBroken?: boolean;
  canReportFixed?: boolean;
  canConfirmBroken?: boolean;
  canRecordMaintenance?: boolean;
  canReportLost?: boolean;
  activeRequestId?: number | null;
  activeRequestStatus?: string | null;
}

export interface LibraryUnitDetail {
  unitId: number;
  offeringId: number;
  holderUserId: number;
  holderUsername: string;
  title: string;
  descriptionPreview: string;
  categories: string[];
  thumbnailResourceId?: string | null;
  thumbnailUrl?: string | null;
  hasEncryptedContent?: boolean;
  unitStatus: string;
  valuePerUnit: number;
  unitLabel?: string | null;
  remainingStock?: number | null;
  quantityNotApplicable?: boolean;
  isOutOfStock?: boolean;
  offeringKind?: string;
  fulfillmentMode?: string;
  brokenPendingConfirmation?: boolean;
  isRetired?: boolean;
  imageUrls?: string[];
  viewer: LibraryUnitViewerContext;
  fullDescription?: string | null;
}

export interface LibraryUnitDetailResponse {
  success: boolean;
  message: string;
  item?: LibraryUnitDetail;
}

export interface CreateLibraryRequestPayload {
  quantity?: number;
  purposePreview: string;
  neededByStart: string;
  neededByEnd: string;
  nonce: string;
  ciphertext: string;
  keyVersion?: number;
}

export interface UpdateLibraryRequestPayload extends CreateLibraryRequestPayload {}

export interface LibraryRequestOperationResponse {
  success: boolean;
  message: string;
  requestId?: number;
}

export interface LibraryRequestListItem {
  requestId: number;
  unitId: number;
  offeringId: number;
  holderUserId: number;
  holderUsername: string;
  requesterUserId: number;
  requesterUsername: string;
  title: string;
  descriptionPreview: string;
  purposePreview: string;
  categories: string[];
  thumbnailResourceId?: string | null;
  thumbnailUrl?: string | null;
  hasEncryptedContent?: boolean;
  hasEncryptedPurpose?: boolean;
  status: string;
  quantity: number;
  neededByStart: string;
  neededByEnd: string;
  createdAt: string;
  fullPurpose?: string | null;
}

export interface LibraryRequestDetail extends LibraryRequestListItem {
  isPossessorView: boolean;
  canEdit: boolean;
  canCancel: boolean;
  canComplete: boolean;
  canDeny: boolean;
  canUndeny: boolean;
  canMessage: boolean;
  openRequestCountOnUnit: number;
}

export interface LibraryCompleteRequestResponse extends LibraryRequestOperationResponse {
  giftId?: number;
}

export interface LibraryRequestMessage {
  id: number;
  authorUserId: number;
  authorUsername: string;
  createdAt: string;
  hasEncryptedContent: boolean;
  encryptedPayload?: {
    keyVersion: number;
    nonce: string;
    ciphertext: string;
  } | null;
  body?: string;
}

export interface LibraryRequestMessageListResponse {
  success: boolean;
  message: string;
  items: LibraryRequestMessage[];
  hasMore: boolean;
}

export interface LibraryRequestMessageOperationResponse {
  success: boolean;
  message: string;
  messageId?: number;
  item?: LibraryRequestMessage;
}

export interface SendLibraryRequestMessagePayload {
  nonce: string;
  ciphertext: string;
  keyVersion?: number;
}

export interface LibraryRequestListResponse {
  success: boolean;
  message: string;
  items: LibraryRequestListItem[];
}

export interface LibraryRequestDetailResponse {
  success: boolean;
  message: string;
  item?: LibraryRequestDetail;
}

export interface RecordLibraryAcquisitionPayload {
  quantity?: number;
  purposePreview: string;
  nonce: string;
  ciphertext: string;
  keyVersion?: number;
}

export interface LibraryOfferingListItem {
  offeringId: number;
  unitId?: number | null;
  title: string;
  descriptionPreview: string;
  categories: string[];
  thumbnailResourceId?: string | null;
  thumbnailUrl?: string | null;
  hasEncryptedContent?: boolean;
  offeringKind: string;
  fulfillmentMode: string;
  remainingStock?: number | null;
  quantityNotApplicable?: boolean;
  isOutOfStock?: boolean;
  valuePerUnit: number;
  unitLabel?: string | null;
  createdAt: string;
}

export interface LibraryOfferingListResponse {
  success: boolean;
  message: string;
  items: LibraryOfferingListItem[];
  hasMore: boolean;
}

export interface LibraryOfferingListPage {
  items: LibraryOfferingListItem[];
  hasMore: boolean;
}

export interface UpdateLibraryOfferingRequest {
  isOutOfStock?: boolean | null;
}

export type LibraryOfferingKind = 'Durable' | 'Consumable' | 'Service';
export type LibraryFulfillmentMode = 'OnRequest' | 'OnDemand';

export interface ReportLibraryUnitBrokenPayload {
  explanationPreview: string;
  nonce: string;
  ciphertext: string;
  keyVersion?: number;
}

export interface RecordLibraryMaintenancePayload {
  cost: number;
  nonce: string;
  ciphertext: string;
  keyVersion?: number;
}

export interface LibraryUnitOperationResponse {
  success: boolean;
  message: string;
  unitId?: number;
}

export interface LibraryMaintenanceOperationResponse {
  success: boolean;
  message: string;
  maintenanceId?: number;
  giftId?: number;
}

export type LibraryHubSection =
  | 'requests'
  | 'durable'
  | 'consumable'
  | 'services'
  | 'mine';
