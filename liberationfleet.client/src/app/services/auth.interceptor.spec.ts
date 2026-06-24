import { TestBed } from '@angular/core/testing';
import { HTTP_INTERCEPTORS, HttpClient } from '@angular/common/http';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuthInterceptor } from './auth.interceptor';
import { AppStorageService, StorageScope } from './storage/app-storage.service';
import { AUTH_TOKEN_STORAGE_KEY } from './storage/storage-keys';
import { clearAuthStorage } from '../testing/test-helpers';

describe('AuthInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let storage: AppStorageService;

  beforeEach(() => {
    clearAuthStorage();

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        AppStorageService,
        { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true }
      ]
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    storage = TestBed.inject(AppStorageService);
  });

  afterEach(() => {
    httpMock.verify();
    clearAuthStorage();
  });

  it('should add Authorization header when token exists', () => {
    storage.set(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY, 'bearer-token');

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
