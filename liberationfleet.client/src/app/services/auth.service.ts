import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import { tap } from 'rxjs/operators';
import { AuthResult, User } from '../models/user.model';
import { CryptoSessionService } from './crypto/crypto-session.service';

export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly apiUrl = '/api/auth';
  private readonly tokenKey = 'auth_token';
  private readonly currentUserSubject = new BehaviorSubject<User | null>(null);
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(private http: HttpClient, private cryptoSession: CryptoSessionService) {
    this.loadToken();
  }

  login(data: LoginRequest): Observable<AuthResult> {
    return this.http.post<AuthResult>(`${this.apiUrl}/login`, data).pipe(
      tap(response => this.establishSession(response))
    );
  }

  establishSession(response: AuthResult): void {
    if (response.token) {
      this.setToken(response.token);
      this.currentUserSubject.next(response.user ?? null);
    }
  }

  logout(): void {
    this.removeToken();
    this.cryptoSession.clearSession();
    this.currentUserSubject.next(null);
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  setToken(token: string): void {
    localStorage.setItem(this.tokenKey, token);
  }

  removeToken(): void {
    localStorage.removeItem(this.tokenKey);
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }

  async initializeEncryption(password: string, isNewAccount: boolean): Promise<void> {
    if (isNewAccount) {
      await this.cryptoSession.provisionIdentityKeys(password);
      return;
    }

    try {
      await this.cryptoSession.unlockFromPassword(password);
    } catch {
      await this.cryptoSession.provisionIdentityKeys(password);
    }
  }

  updateCurrentUser(user: User): void {
    this.currentUserSubject.next(user);
  }

  private loadToken(): void {
    const token = this.getToken();
    if (token) {
      this.currentUserSubject.next(null);
    }
  }
}
