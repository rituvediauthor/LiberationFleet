import { TestBed } from '@angular/core/testing';
import { HTTP_INTERCEPTORS, HttpClient } from '@angular/common/http';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuthInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { clearAuthStorage } from '../testing/test-helpers';

describe('AuthInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  beforeEach(() => {
    clearAuthStorage();

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        AuthService,
        { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true }
      ]
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => {
    httpMock.verify();
    clearAuthStorage();
  });

  it('should add Authorization header when token exists', () => {
    authService.setToken('bearer-token');

    http.get('/api/protected').subscribe();

    const req = httpMock.expectOne('/api/protected');
    expect(req.request.headers.get('Authorization')).toBe('Bearer bearer-token');
    req.flush({});
  });

  it('should not add Authorization header when token is missing', () => {
    http.get('/api/public').subscribe();

    const req = httpMock.expectOne('/api/public');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });
});
