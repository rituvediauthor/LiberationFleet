import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthResult } from '../models/user.model';

export interface AccountRecoveryChallengeResponse {
  success: boolean;
  message: string;
  challengeToken?: string | null;
  maskedEmail?: string | null;
  oldUsername?: string | null;
}

export interface AccountRecoveryCompleteResponse extends AuthResult {
  recoveredUsername?: string | null;
  absorbedUsername?: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class AccountRecoveryService {
  private readonly apiUrl = '/api/account-recovery';

  constructor(private http: HttpClient) {}

  begin(oldUserId: number): Observable<AccountRecoveryChallengeResponse> {
    return this.http.post<AccountRecoveryChallengeResponse>(`${this.apiUrl}/begin`, { oldUserId });
  }

  resend(challengeToken: string): Observable<AccountRecoveryChallengeResponse> {
    return this.http.post<AccountRecoveryChallengeResponse>(`${this.apiUrl}/resend`, { challengeToken });
  }

  complete(
    oldUserId: number,
    challengeToken: string,
    code: string,
    password: string
  ): Observable<AccountRecoveryCompleteResponse> {
    return this.http.post<AccountRecoveryCompleteResponse>(`${this.apiUrl}/complete`, {
      oldUserId,
      challengeToken,
      code,
      password
    });
  }
}
