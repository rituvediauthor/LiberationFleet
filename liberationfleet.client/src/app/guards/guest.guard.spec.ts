import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { guestGuard } from './guest.guard';
import { AuthService } from '../services/auth.service';
import { createAuthServiceMock } from '../testing/test-helpers';

describe('guestGuard', () => {
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

  it('should allow sign-in when not authenticated', () => {
    authService.isAuthenticated.and.returnValue(false);

    const result = TestBed.runInInjectionContext(() => guestGuard({} as never, {} as never));

    expect(result).toBeTrue();
  });

  it('should redirect to crew dashboard when authenticated', () => {
    authService.isAuthenticated.and.returnValue(true);

    TestBed.runInInjectionContext(() => guestGuard({} as never, {} as never));

    expect(router.createUrlTree).toHaveBeenCalledWith(['/app/crew']);
  });
});
