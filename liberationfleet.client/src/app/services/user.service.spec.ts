import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { UserService } from './user.service';

describe('UserService', () => {
  let service: UserService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [UserService]
    });

    service = TestBed.inject(UserService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('create should post registration data', () => {
    const payload = {
      username: 'user',
      email: 'user@example.com',
      password: 'Password1!',
      confirmPassword: 'Password1!'
    };

    service.create(payload).subscribe();

    const req = httpMock.expectOne('/api/auth/register');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ success: true, token: 'jwt', user: { id: 1, username: 'user', email: 'user@example.com' } });
  });

  it('requestPasswordReset should post email', () => {
    service.requestPasswordReset('user@example.com').subscribe();

    const req = httpMock.expectOne('/api/auth/request-password-reset');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'user@example.com' });
    req.flush({ success: true, message: 'Email sent' });
  });

  it('validateResetToken should post token', () => {
    service.validateResetToken('reset-token').subscribe();

    const req = httpMock.expectOne('/api/auth/validate-reset-token');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ token: 'reset-token' });
    req.flush({ isValid: true, message: 'Token is valid', email: 'user@example.com' });
  });

  it('resetPassword should post reset payload', () => {
    const payload = { token: 't', newPassword: 'Password1!', confirmPassword: 'Password1!' };
    service.resetPassword(payload).subscribe();

    const req = httpMock.expectOne('/api/auth/reset-password');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ success: true, message: 'Password reset successfully' });
  });
});
