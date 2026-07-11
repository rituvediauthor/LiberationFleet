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

  getProposals(status: ProposalStatus): Observable<ProposalListItem[]> {
    return this.http.get<ProposalListResponse>(this.apiUrl, { params: { status } }).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load proposals');
        }
        return response.items.map(item => this.mapListItem(item));
      })
    );
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
          createdAt: new Date(comment.createdAt)
        }));
      })
    );
  }

  createProposal(payload: EncryptedContentSendPayload): Observable<ProposalOperationResponse> {
    return this.http.post<ProposalOperationResponse>(this.apiUrl, {
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1,
      mentionedUserIds: payload.mentionedUserIds ?? []
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
    payload: EncryptedContentSendPayload & { parentCommentId?: number | null }
  ): Observable<ProposalOperationResponse> {
    return this.http.post<ProposalOperationResponse>(`${this.apiUrl}/${proposalId}/comments`, {
      parentCommentId: payload.parentCommentId ?? null,
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1,
      mentionedUserIds: payload.mentionedUserIds ?? []
    });
  }

  updateComment(
    proposalId: number,
    commentId: number,
    payload: EncryptedContentSendPayload
  ): Observable<ProposalOperationResponse> {
    return this.http.put<ProposalOperationResponse>(`${this.apiUrl}/${proposalId}/comments/${commentId}`, {
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
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
      return 'Awaiting auto-approval';
    }
    const totalMinutes = Math.floor(diffMs / 60000);
    const days = Math.floor(totalMinutes / (60 * 24));
    const hours = Math.floor((totalMinutes % (60 * 24)) / 60);
    const minutes = totalMinutes % 60;
    if (days > 0) {
      return `${days}d ${hours}h ${minutes}m`;
    }
    if (hours > 0) {
      return `${hours}h ${minutes}m`;
    }
    return `${minutes}m`;
  }

  private mapListItem(item: ProposalListItem): ProposalListItem {
    return {
      ...item,
      lastActivityAt: new Date(item.lastActivityAt),
      approvalTimerEndsAt: item.approvalTimerEndsAt ? new Date(item.approvalTimerEndsAt) : null
    };
  }

  private mapDetail(proposal: ProposalDetail): ProposalDetail {
    return {
      ...this.mapListItem(proposal),
      createdAt: new Date(proposal.createdAt),
      canEdit: proposal.canEdit,
      canDelete: proposal.canDelete,
      canVote: proposal.canVote ?? true,
      isKickVoteTarget: proposal.isKickVoteTarget ?? false,
      usesAnonymousComments: proposal.usesAnonymousComments,
      viewerAlias: proposal.viewerAlias,
      canKickAuthor: proposal.canKickAuthor,
      comments: (proposal.comments ?? []).map(comment => ({
        ...comment,
        createdAt: new Date(comment.createdAt)
      }))
    };
  }
}
