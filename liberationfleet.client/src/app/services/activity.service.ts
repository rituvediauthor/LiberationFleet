import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { UserActivityFilterCategory, UserActivityListResponse } from '../models/activity.model';

@Injectable({
  providedIn: 'root'
})
export class ActivityService {
  private readonly apiUrl = '/api/activity';

  constructor(private http: HttpClient) {}

  getActivity(
    category: UserActivityFilterCategory,
    options?: { beforeCreatedAt?: string; beforeKey?: string; limit?: number }
  ): Observable<UserActivityListResponse> {
    let params = new HttpParams().set('category', category);
    if (options?.beforeCreatedAt) {
      params = params.set('beforeCreatedAt', options.beforeCreatedAt);
    }
    if (options?.beforeKey) {
      params = params.set('beforeKey', options.beforeKey);
    }
    if (options?.limit != null) {
      params = params.set('limit', String(options.limit));
    }

    return this.http.get<UserActivityListResponse>(this.apiUrl, { params });
  }
}
