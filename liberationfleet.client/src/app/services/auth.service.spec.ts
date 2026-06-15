import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { clearAuthStorage } from '../testing/test-helpers';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    clearAuthStorage();

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthService]
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    clearAuthStorage();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('login should post credentials and store token on success', () => {
    const response = {
      success: true,
      token: 'login-token',
      user: { id: 2, username: 'fleet', email: 'fleet@example.com' }
    };

    service.login({ usernameOrEmail: 'fleet@example.com', password: 'password123' }).subscribe();

    const req = httpMock.expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ usernameOrEmail: 'fleet@example.com', password: 'password123' });
    req.flush(response);

    expect(service.getToken()).toBe('login-token');
  });

  it('establishSession should store token and user', () => {
    let latestUser: unknown = 'unset';
    service.currentUser$.subscribe(user => latestUser = user);

    service.establishSession({
      success: true,
      token: 'jwt-token',
      user: { id: 1, username: 'user', email: 'user@example.com' }
    });

    expect(service.getToken()).toBe('jwt-token');
    expect(service.isAuthenticated()).toBeTrue();
    expect(latestUser).toEqual({ id: 1, username: 'user', email: 'user@example.com' });
  });

  it('establishSession should not store token when response has no token', () => {
    service.establishSession({ success: false, message: 'Failed' });
    expect(service.getToken()).toBeNull();
  });

  it('logout should clear token and current user', () => {
    service.setToken('token-to-clear');
    let latestUser: unknown = 'unset';

    service.currentUser$.subscribe(user => latestUser = user);
    service.logout();

    expect(service.getToken()).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
    expect(latestUser).toBeNull();
  });

  it('setToken and removeToken should manage localStorage', () => {
    service.setToken('abc');
    expect(localStorage.getItem('auth_token')).toBe('abc');

    service.removeToken();
    expect(localStorage.getItem('auth_token')).toBeNull();
  });
});

describe('AuthService token loading', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    clearAuthStorage();
    localStorage.setItem('auth_token', 'stored-token');

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthService]
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    clearAuthStorage();
  });

  it('should recognize existing token from localStorage on init', () => {
    expect(service.getToken()).toBe('stored-token');
    expect(service.isAuthenticated()).toBeTrue();
  });
});
