import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AuthResult,
  CreateUserRequest,
  PasswordResetResult,
  ResetPasswordRequest,
  ValidateResetTokenResult
} from '../models/user.model';

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private readonly apiUrl = '/api/auth';

  constructor(private http: HttpClient) {}

  create(request: CreateUserRequest): Observable<AuthResult> {
    return this.http.post<AuthResult>(`${this.apiUrl}/register`, request);
  }

  requestPasswordReset(email: string): Observable<PasswordResetResult> {
    return this.http.post<PasswordResetResult>(`${this.apiUrl}/request-password-reset`, { email });
  }

  validateResetToken(token: string): Observable<ValidateResetTokenResult> {
    return this.http.post<ValidateResetTokenResult>(`${this.apiUrl}/validate-reset-token`, { token });
  }

  resetPassword(request: ResetPasswordRequest): Observable<PasswordResetResult> {
    return this.http.post<PasswordResetResult>(`${this.apiUrl}/reset-password`, request);
  }
}
