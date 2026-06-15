import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ResetPasswordComponent } from './reset-password.component';
import { UserService } from '../../services/user.service';
import { ToastService } from '../../components/toast/toast.component';
import { createUserServiceMock, createToastServiceMock, validSignUpPassword } from '../../testing/test-helpers';

describe('ResetPasswordComponent', () => {
  let fixture: ComponentFixture<ResetPasswordComponent>;
  let component: ResetPasswordComponent;
  let userService: jasmine.SpyObj<UserService>;
  let toastService: jasmine.SpyObj<ToastService>;
  let router: Router;

  async function setup(queryParams: Record<string, string> = { token: 'valid-token' }) {
    userService = createUserServiceMock();
    toastService = createToastServiceMock();

    userService.validateResetToken.and.returnValue(of({
      isValid: true,
      message: 'Token is valid',
      email: 'user@example.com'
    }));

    await TestBed.configureTestingModule({
      imports: [ResetPasswordComponent],
      providers: [
        provideRouter([]),
        { provide: UserService, useValue: userService },
        { provide: ToastService, useValue: toastService },
        {
          provide: ActivatedRoute,
          useValue: { queryParams: of(queryParams) }
        }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(ResetPasswordComponent);
    component = fixture.componentInstance;
    component.ngOnInit();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await setup();
  });

  it('should validate token on init and show email', () => {
    expect(userService.validateResetToken).toHaveBeenCalledWith('valid-token');
    expect(component.tokenEmail).toBe('user@example.com');
    expect(component.tokenError).toBeNull();
  });

  it('hasPasswordRequirement should evaluate password rules', () => {
    component.form.get('newPassword')?.setValue(validSignUpPassword);
    expect(component.hasPasswordRequirement('uppercase')).toBeTrue();
    expect(component.hasPasswordRequirement('special')).toBeTrue();
  });

  it('should reset password and navigate to sign-in on success', () => {
    userService.resetPassword.and.returnValue(of({ success: true, message: 'Password reset successfully' }));

    component.form.setValue({
      newPassword: validSignUpPassword,
      confirmPassword: validSignUpPassword
    });

    component.onSubmit();

    expect(userService.resetPassword).toHaveBeenCalledWith({
      token: 'valid-token',
      newPassword: validSignUpPassword,
      confirmPassword: validSignUpPassword
    });
    expect(toastService.success).toHaveBeenCalledWith('Password reset successfully!');
    expect(router.navigate).toHaveBeenCalledWith(['/sign-in']);
  });

  it('should show error toast when reset fails', () => {
    userService.resetPassword.and.returnValue(throwError(() => ({ error: { message: 'Invalid token' } })));

    component.form.setValue({
      newPassword: validSignUpPassword,
      confirmPassword: validSignUpPassword
    });

    component.onSubmit();

    expect(toastService.error).toHaveBeenCalledWith('Invalid token');
    expect(component.isLoading).toBeFalse();
  });

  it('should navigate back to sign-in', () => {
    component.backButton.onClick?.();
    expect(router.navigate).toHaveBeenCalledWith(['/sign-in']);
  });
});

describe('ResetPasswordComponent token errors', () => {
  it('should show error when token is missing', async () => {
    const userService = createUserServiceMock();

    await TestBed.configureTestingModule({
      imports: [ResetPasswordComponent],
      providers: [
        provideRouter([]),
        { provide: UserService, useValue: userService },
        { provide: ToastService, useValue: createToastServiceMock() },
        { provide: ActivatedRoute, useValue: { queryParams: of({}) } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ResetPasswordComponent);
    fixture.componentInstance.ngOnInit();
    fixture.detectChanges();

    expect(fixture.componentInstance.tokenError).toContain('No reset token provided');
    expect(userService.validateResetToken).not.toHaveBeenCalled();
  });

  it('should disable form when token validation fails', async () => {
    const userService = createUserServiceMock();
    userService.validateResetToken.and.returnValue(of({
      isValid: false,
      message: 'Token expired'
    }));

    await TestBed.configureTestingModule({
      imports: [ResetPasswordComponent],
      providers: [
        provideRouter([]),
        { provide: UserService, useValue: userService },
        { provide: ToastService, useValue: createToastServiceMock() },
        { provide: ActivatedRoute, useValue: { queryParams: of({ token: 'expired-token' }) } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ResetPasswordComponent);
    fixture.componentInstance.ngOnInit();
    fixture.detectChanges();

    expect(fixture.componentInstance.tokenError).toBe('Token expired');
    expect(fixture.componentInstance.form.disabled).toBeTrue();
  });
});
