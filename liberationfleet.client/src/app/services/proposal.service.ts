import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import {
  ProposalCommentRepliesResponse,
  ProposalDetail,
  ProposalDetailResponse,
  ProposalListItem,
  ProposalListResponse,
  ProposalOperationResponse,
  ProposalStatus,
  ProposalVoteChoice
} from '../models/proposal.model';
import { EncryptedContentSendPayload } from '../models/encrypted-send.model';

@Injectable({
  providedIn: 'root'
})
export class ProposalService {
  private readonly apiUrl = '/api/proposals';

  constructor(private http: HttpClient) {}

  getProposals(status: ProposalStatus, scope: 'crew' | 'fleet' = 'crew'): Observable<ProposalListItem[]> {
    const params: Record<string, string> = { status };
    if (scope === 'fleet') {
      params['scope'] = 'fleet';
    }
    return this.http.get<ProposalListResponse>(this.apiUrl, { params }).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load proposals');
        }
        return response.items.map(item => this.mapListItem(item));
      })
    );
  }

  createFleetProposal(payload: EncryptedContentSendPayload): Observable<ProposalOperationResponse> {
    return this.http.post<ProposalOperationResponse>(this.apiUrl, {
      scope: 'fleet',
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1,
      mentionedUserIds: payload.mentionedUserIds ?? [],
      title: payload.title ?? '',
      description: payload.description ?? payload.notificationPreview ?? ''
    });
  }

  getProposal(id: number): Observable<ProposalDetail> {
    return this.http.get<ProposalDetailResponse>(`${this.apiUrl}/${id}`).pipe(
      map(response => {
        if (!response.success || !response.proposal) {
          throw new Error(response.message || 'Failed to load proposal');
        }
        return this.mapDetail(response.proposal);
      })
    );
  }

  getCommentReplies(proposalId: number, parentCommentId: number) {
    return this.http.get<ProposalCommentRepliesResponse>(
      `${this.apiUrl}/${proposalId}/comments/${parentCommentId}/replies`
    ).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load replies');
        }
        return response.items.map(comment => ({
          ...comment,
          createdAt: this.parseApiDate(comment.createdAt)
        }));
      })
    );
  }

  createProposal(payload: EncryptedContentSendPayload): Observable<ProposalOperationResponse> {
    return this.http.post<ProposalOperationResponse>(this.apiUrl, {
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1,
      mentionedUserIds: payload.mentionedUserIds ?? [],
      title: payload.title ?? '',
      description: payload.description ?? payload.notificationPreview ?? ''
    });
  }

  updateProposal(
    id: number,
    payload: EncryptedContentSendPayload
  ): Observable<ProposalOperationResponse> {
    return this.http.put<ProposalOperationResponse>(`${this.apiUrl}/${id}`, {
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1,
      mentionedUserIds: payload.mentionedUserIds ?? []
    });
  }

  deleteProposal(id: number): Observable<ProposalOperationResponse> {
    return this.http.delete<ProposalOperationResponse>(`${this.apiUrl}/${id}`);
  }

  vote(proposalId: number, vote: ProposalVoteChoice): Observable<ProposalOperationResponse> {
    return this.http.post<ProposalOperationResponse>(`${this.apiUrl}/${proposalId}/vote`, { vote });
  }

  postComment(
    proposalId: number,
    payload: EncryptedContentSendPayload & { parentCommentId?: number | null; body?: string }
  ): Observable<ProposalOperationResponse> {
    return this.http.post<ProposalOperationResponse>(`${this.apiUrl}/${proposalId}/comments`, {
      parentCommentId: payload.parentCommentId ?? null,
      body: payload.body ?? payload.notificationPreview ?? null,
      nonce: payload.nonce ?? '',
      ciphertext: payload.ciphertext ?? '',
      keyVersion: payload.keyVersion ?? 1,
      mentionedUserIds: payload.mentionedUserIds ?? []
    });
  }

  updateComment(
    proposalId: number,
    commentId: number,
    payload: EncryptedContentSendPayload & { body?: string }
  ): Observable<ProposalOperationResponse> {
    return this.http.put<ProposalOperationResponse>(`${this.apiUrl}/${proposalId}/comments/${commentId}`, {
      body: payload.body,
      nonce: payload.nonce ?? '',
      ciphertext: payload.ciphertext ?? '',
      keyVersion: payload.keyVersion ?? 1,
      mentionedUserIds: payload.mentionedUserIds ?? []
    });
  }

  rerollAlias(proposalId: number): Observable<ProposalOperationResponse> {
    return this.http.post<ProposalOperationResponse>(`${this.apiUrl}/${proposalId}/alias/reroll`, {});
  }

  kickFromComment(proposalId: number, commentId: number, reason: string): Observable<ProposalOperationResponse> {
    return this.http.post<ProposalOperationResponse>(
      `${this.apiUrl}/${proposalId}/comments/${commentId}/kick`,
      { reason }
    );
  }

  kickFromProposalAuthor(proposalId: number, reason: string): Observable<ProposalOperationResponse> {
    return this.http.post<ProposalOperationResponse>(`${this.apiUrl}/${proposalId}/author/kick`, { reason });
  }

  formatCountdown(endAt?: Date | null): string | null {
    if (!endAt) {
      return null;
    }
    const diffMs = endAt.getTime() - Date.now();
    if (diffMs <= 0) {
      return 'Awaiting auto-resolution';
    }
    const totalMinutes = Math.floor(diffMs / 60000);
    const days = Math.floor(totalMinutes / (60 * 24));
    const hours = Math.floor((totalMinutes % (60 * 24)) / 60);
    const minutes = totalMinutes % 60;
    const parts: string[] = [];
    if (days > 0) {
      parts.push(`${days} day${days === 1 ? '' : 's'}`);
    }
    if (hours > 0) {
      parts.push(`${hours} hour${hours === 1 ? '' : 's'}`);
    }
    if (minutes > 0 || parts.length === 0) {
      parts.push(`${minutes} minute${minutes === 1 ? '' : 's'}`);
    }
    if (parts.length === 1) {
      return parts[0];
    }
    if (parts.length === 2) {
      return `${parts[0]} and ${parts[1]}`;
    }
    return `${parts[0]}, ${parts[1]}, and ${parts[2]}`;
  }

  parseApiDate(value: string | Date): Date {
    if (value instanceof Date) {
      return value;
    }
    // Unspecified timestamps from older responses are UTC wall times — force Z.
    if (/^\d{4}-\d{2}-\d{2}T/.test(value) && !/(Z|[+-]\d{2}:?\d{2})$/.test(value)) {
      return new Date(`${value}Z`);
    }
    return new Date(value);
  }

  private mapListItem(item: ProposalListItem): ProposalListItem {
    return {
      ...item,
      kind: item.kind,
      lastActivityAt: this.parseApiDate(item.lastActivityAt),
      approvalTimerEndsAt: item.approvalTimerEndsAt
        ? this.parseApiDate(item.approvalTimerEndsAt as string | Date)
        : null
    };
  }

  private mapDetail(proposal: ProposalDetail): ProposalDetail {
    return {
      ...this.mapListItem(proposal),
      createdAt: this.parseApiDate(proposal.createdAt),
      canEdit: proposal.canEdit,
      canDelete: proposal.canDelete,
      canVote: proposal.canVote ?? true,
      isKickVoteTarget: proposal.isKickVoteTarget ?? false,
      usesAnonymousComments: proposal.usesAnonymousComments,
      viewerAlias: proposal.viewerAlias,
      aliasRerollsRemaining: proposal.aliasRerollsRemaining,
      canKickAuthor: proposal.canKickAuthor,
      comments: (proposal.comments ?? []).map(comment => ({
        ...comment,
        createdAt: this.parseApiDate(comment.createdAt)
      }))
    };
  }
}
