export type ContentReportReason =
  | 'ChildSexualExploitation'
  | 'NonConsensualIntimateImage'
  | 'ThreatsOfViolence'
  | 'OtherIllegal'
  | 'Harassment'
  | 'Spam';

export type ContentReportTargetType =
  | 'ChatMessage'
  | 'ForumPost'
  | 'ForumComment'
  | 'ProposalComment'
  | 'UserProfile'
  | 'DirectMessage'
  | 'Proposal';

export interface CreateContentReportRequest {
  reason: ContentReportReason;
  targetType: ContentReportTargetType;
  targetResourceId?: number | null;
  targetParentId?: number | null;
  targetAuthorUserId?: number | null;
  crewId?: number | null;
  fleetId?: number | null;
  reporterNote?: string | null;
  evidencePlaintextJson: string;
  alsoBlockAuthor?: boolean;
}

export interface CreateContentReportResponse {
  success: boolean;
  message: string;
  reportId?: number;
  status?: string;
}

export interface ReportEvidenceSnapshot {
  text?: string | null;
  title?: string | null;
  authorUsername?: string | null;
  mediaResourceIds?: string[];
  attestation: string;
  reportedAtClient: string;
}

export const CONTENT_REPORT_REASONS: { value: ContentReportReason; label: string; serious?: boolean }[] = [
  {
    value: 'ChildSexualExploitation',
    label: 'Child sexual exploitation / CSAM / grooming',
    serious: true
  },
  { value: 'NonConsensualIntimateImage', label: 'Non-consensual intimate image' },
  { value: 'ThreatsOfViolence', label: 'Threats of violence / imminent harm' },
  { value: 'OtherIllegal', label: 'Other illegal content' },
  { value: 'Harassment', label: 'Harassment / abuse' },
  { value: 'Spam', label: 'Spam / scam' }
];
