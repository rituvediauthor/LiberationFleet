import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { DiscussionConfig } from '../config/discussion.config';
import { EncryptedContentSendPayload } from '../models/encrypted-send.model';
import {
  DiscussionComment,
  DiscussionDetail,
  DiscussionDetailResponse,
  DiscussionListItem,
  DiscussionListResponse,
  DiscussionOperationResponse,
  DiscussionCommentRepliesResponse,
  ForumLikeToggleResponse
} from '../models/crew-discussion.model';
import { ContentLiker, ContentLikersResponse } from '../models/gift.model';

@Injectable({
  providedIn: 'root'
})
export class CrewDiscussionService {
  constructor(private http: HttpClient) {}

  getPosts(
    config: DiscussionConfig,
    options?: { offset?: number; limit?: number }
  ): Observable<{ items: DiscussionListItem[]; hasMore: boolean }> {
    const params: Record<string, string> = {};
    if (options?.offset != null) {
      params['offset'] = String(options.offset);
    }
    if (options?.limit != null) {
      params['limit'] = String(options.limit);
    }
    return this.http.get<DiscussionListResponse>(config.apiPath, { params }).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || `Failed to load ${config.labelPlural.toLowerCase()}`);
        }
        return {
          items: response.items.map(item => this.mapListItem(item)),
          hasMore: !!response.hasMore
        };
      })
    );
  }

  getPost(config: DiscussionConfig, id: number): Observable<DiscussionDetail> {
    return this.http.get<DiscussionDetailResponse>(`${config.apiPath}/${id}`).pipe(
      map(response => {
        if (!response.success || !response.post) {
          throw new Error(response.message || `Failed to load ${config.postLabel}`);
        }
        return this.mapDetail(response.post);
      })
    );
  }

  getCommentReplies(config: DiscussionConfig, postId: number, parentCommentId: number) {
    return this.http.get<DiscussionCommentRepliesResponse>(
      `${config.apiPath}/${postId}/comments/${parentCommentId}/replies`
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

  createPost(
    config: DiscussionConfig,
    payload: EncryptedContentSendPayload & { isAdultContent?: boolean }
  ): Observable<DiscussionOperationResponse> {
    return this.http.post<DiscussionOperationResponse>(config.apiPath, {
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1,
      isAdultContent: payload.isAdultContent ?? false,
      mentionedUserIds: payload.mentionedUserIds ?? [],
      notificationPreview: payload.notificationPreview ?? payload.description ?? null
    });
  }

  updatePost(
    config: DiscussionConfig,
    id: number,
    payload: EncryptedContentSendPayload
  ): Observable<DiscussionOperationResponse> {
    return this.http.put<DiscussionOperationResponse>(`${config.apiPath}/${id}`, {
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1,
      mentionedUserIds: payload.mentionedUserIds ?? []
    });
  }

  deletePost(config: DiscussionConfig, id: number): Observable<DiscussionOperationResponse> {
    return this.http.delete<DiscussionOperationResponse>(`${config.apiPath}/${id}`);
  }

  postComment(
    config: DiscussionConfig,
    postId: number,
    payload: EncryptedContentSendPayload & { parentCommentId?: number | null }
  ): Observable<DiscussionOperationResponse> {
    return this.http.post<DiscussionOperationResponse>(`${config.apiPath}/${postId}/comments`, {
      parentCommentId: payload.parentCommentId ?? null,
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1,
      mentionedUserIds: payload.mentionedUserIds ?? [],
      notificationPreview: payload.notificationPreview ?? payload.body ?? null
    });
  }

  updateComment(
    config: DiscussionConfig,
    postId: number,
    commentId: number,
    payload: EncryptedContentSendPayload
  ): Observable<DiscussionOperationResponse> {
    return this.http.put<DiscussionOperationResponse>(`${config.apiPath}/${postId}/comments/${commentId}`, {
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1,
      mentionedUserIds: payload.mentionedUserIds ?? []
    });
  }

  togglePostLike(config: DiscussionConfig, postId: number): Observable<ForumLikeToggleResponse> {
    return this.http.post<ForumLikeToggleResponse>(`${config.apiPath}/${postId}/like`, {});
  }

  toggleCommentLike(
    config: DiscussionConfig,
    postId: number,
    commentId: number
  ): Observable<ForumLikeToggleResponse> {
    return this.http.post<ForumLikeToggleResponse>(
      `${config.apiPath}/${postId}/comments/${commentId}/like`,
      {}
    );
  }

  getPostLikers(config: DiscussionConfig, postId: number): Observable<ContentLiker[]> {
    return this.http.get<ContentLikersResponse>(`${config.apiPath}/${postId}/likers`).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load likers');
        }
        return response.items ?? [];
      })
    );
  }

  getCommentLikers(
    config: DiscussionConfig,
    postId: number,
    commentId: number
  ): Observable<ContentLiker[]> {
    return this.http.get<ContentLikersResponse>(
      `${config.apiPath}/${postId}/comments/${commentId}/likers`
    ).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load likers');
        }
        return response.items ?? [];
      })
    );
  }

  private mapListItem(item: DiscussionListItem): DiscussionListItem {
    return {
      ...item,
      lastActivityAt: new Date(item.lastActivityAt)
    };
  }

  private mapDetail(post: DiscussionDetail): DiscussionDetail {
    return {
      ...this.mapListItem(post),
      createdAt: new Date(post.createdAt),
      canEdit: post.canEdit,
      canDelete: post.canDelete,
      comments: (post.comments ?? []).map((comment: DiscussionComment) => ({
        ...comment,
        createdAt: new Date(comment.createdAt)
      }))
    };
  }
}
