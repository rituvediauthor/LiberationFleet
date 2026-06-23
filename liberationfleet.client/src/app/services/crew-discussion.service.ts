import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { DiscussionConfig } from '../config/discussion.config';
import {
  DiscussionComment,
  DiscussionDetail,
  DiscussionDetailResponse,
  DiscussionListItem,
  DiscussionListResponse,
  DiscussionOperationResponse,
  DiscussionCommentRepliesResponse
} from '../models/crew-discussion.model';

@Injectable({
  providedIn: 'root'
})
export class CrewDiscussionService {
  constructor(private http: HttpClient) {}

  getPosts(config: DiscussionConfig): Observable<DiscussionListItem[]> {
    return this.http.get<DiscussionListResponse>(config.apiPath).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || `Failed to load ${config.labelPlural.toLowerCase()}`);
        }
        return response.items.map(item => this.mapListItem(item));
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
    payload: { nonce: string; ciphertext: string; keyVersion?: number }
  ): Observable<DiscussionOperationResponse> {
    return this.http.post<DiscussionOperationResponse>(config.apiPath, {
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1
    });
  }

  updatePost(
    config: DiscussionConfig,
    id: number,
    payload: { nonce: string; ciphertext: string; keyVersion?: number }
  ): Observable<DiscussionOperationResponse> {
    return this.http.put<DiscussionOperationResponse>(`${config.apiPath}/${id}`, {
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1
    });
  }

  deletePost(config: DiscussionConfig, id: number): Observable<DiscussionOperationResponse> {
    return this.http.delete<DiscussionOperationResponse>(`${config.apiPath}/${id}`);
  }

  postComment(
    config: DiscussionConfig,
    postId: number,
    payload: { parentCommentId?: number | null; nonce: string; ciphertext: string; keyVersion?: number }
  ): Observable<DiscussionOperationResponse> {
    return this.http.post<DiscussionOperationResponse>(`${config.apiPath}/${postId}/comments`, {
      parentCommentId: payload.parentCommentId ?? null,
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1
    });
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
