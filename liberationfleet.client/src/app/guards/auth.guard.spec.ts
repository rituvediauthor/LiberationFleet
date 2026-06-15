import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';
import { createAuthServiceMock } from '../testing/test-helpers';

describe('authGuard', () => {
  let authService: jasmine.SpyObj<AuthService>;
  let router: Router;

  beforeEach(() => {
    authService = createAuthServiceMock();

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authService }
      ]
    });

    router = TestBed.inject(Router);
    spyOn(router, 'createUrlTree').and.callThrough();
  });

  it('should allow access when authenticated', () => {
    authService.isAuthenticated.and.returnValue(true);

    const result = TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));

    expect(result).toBeTrue();
  });

  it('should redirect to sign-in when not authenticated', () => {
    authService.isAuthenticated.and.returnValue(false);

    TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));

    expect(router.createUrlTree).toHaveBeenCalledWith(['/sign-in']);
  });
});
