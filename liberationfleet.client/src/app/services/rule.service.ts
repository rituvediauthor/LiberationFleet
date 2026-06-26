import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  RuleDeletePayload,
  RuleDetailResponse,
  RuleListResponse,
  RuleOperationResponse,
  RuleWritePayload
} from '../models/rule.model';

@Injectable({
  providedIn: 'root'
})
export class RuleService {
  private readonly apiUrl = '/api/rules';

  constructor(private http: HttpClient) {}

  getRules(): Observable<RuleListResponse> {
    return this.http.get<RuleListResponse>(this.apiUrl);
  }

  getRule(id: number): Observable<RuleDetailResponse> {
    return this.http.get<RuleDetailResponse>(`${this.apiUrl}/${id}`);
  }

  createRule(payload: RuleWritePayload): Observable<RuleOperationResponse> {
    return this.http.post<RuleOperationResponse>(this.apiUrl, {
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1,
      plaintextTitle: payload.plaintextTitle,
      plaintextDescription: payload.plaintextDescription
    });
  }

  updateRule(id: number, payload: RuleWritePayload): Observable<RuleOperationResponse> {
    return this.http.put<RuleOperationResponse>(`${this.apiUrl}/${id}`, {
      nonce: payload.nonce,
      ciphertext: payload.ciphertext,
      keyVersion: payload.keyVersion ?? 1,
      plaintextTitle: payload.plaintextTitle,
      plaintextDescription: payload.plaintextDescription,
      plaintextOldTitle: payload.plaintextOldTitle ?? '',
      plaintextOldDescription: payload.plaintextOldDescription ?? ''
    });
  }

  deleteRule(id: number, payload: RuleDeletePayload): Observable<RuleOperationResponse> {
    return this.http.delete<RuleOperationResponse>(`${this.apiUrl}/${id}`, { body: payload });
  }
}
