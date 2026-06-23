import { TestBed } from '@angular/core/testing';
import { HTTP_INTERCEPTORS, HttpClient } from '@angular/common/http';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ApiBaseUrlInterceptor } from './api-base-url.interceptor';
import { APP_ENVIRONMENT } from '../config/app-environment';

describe('ApiBaseUrlInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  function configure(apiBaseUrl: string) {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        { provide: APP_ENVIRONMENT, useValue: { production: true, apiBaseUrl } },
        { provide: HTTP_INTERCEPTORS, useClass: ApiBaseUrlInterceptor, multi: true }
      ]
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => {
    httpMock.verify();
  });

  it('should leave relative API URLs unchanged when apiBaseUrl is empty', () => {
    configure('');
    http.get('/api/auth/login').subscribe();
    const req = httpMock.expectOne('/api/auth/login');
    expect(req.request.url).toBe('/api/auth/login');
  });

  it('should prefix API URLs when apiBaseUrl is configured', () => {
    configure('https://api.example.com');
    http.get('/api/crews/membership').subscribe();
    const req = httpMock.expectOne('https://api.example.com/api/crews/membership');
    expect(req.request.url).toBe('https://api.example.com/api/crews/membership');
  });

  it('should not rewrite non-API URLs', () => {
    configure('https://api.example.com');
    http.get('/assets/privacy-policy.txt').subscribe();
    const req = httpMock.expectOne('/assets/privacy-policy.txt');
    expect(req.request.url).toBe('/assets/privacy-policy.txt');
  });
});
