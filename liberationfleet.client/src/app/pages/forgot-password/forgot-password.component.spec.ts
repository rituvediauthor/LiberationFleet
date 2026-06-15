import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ForgotPasswordComponent } from './forgot-password.component';
import { UserService } from '../../services/user.service';
import { ToastService } from '../../components/toast/toast.component';
import { createUserServiceMock, createToastServiceMock } from '../../testing/test-helpers';

describe('ForgotPasswordComponent', () => {
  let fixture: ComponentFixture<ForgotPasswordComponent>;
  let component: ForgotPasswordComponent;
  let userService: jasmine.SpyObj<UserService>;
  let toastService: jasmine.SpyObj<ToastService>;
  let router: Router;

  beforeEach(async () => {
    userService = createUserServiceMock();
    toastService = createToastServiceMock();

    await TestBed.configureTestingModule({
      imports: [ForgotPasswordComponent],
      providers: [
        provideRouter([]),
        { provide: UserService, useValue: userService },
        { provide: ToastService, useValue: toastService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(ForgotPasswordComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create with send button disabled until form is valid', () => {
    expect(component.form.invalid).toBeTrue();
    expect(component.sendButton.disabled).toBeTrue();
  });

  it('should enable send button when email is valid', () => {
    component.form.setValue({ email: 'user@example.com' });
    fixture.detectChanges();

    expect(component.sendButton.disabled).toBeFalse();
  });

  it('should request password reset and show success toast', () => {
    userService.requestPasswordReset.and.returnValue(of({ success: true, message: 'Recovery email sent' }));
    component.form.setValue({ email: 'user@example.com' });

    component.onSubmit();

    expect(userService.requestPasswordReset).toHaveBeenCalledWith('user@example.com');
    expect(toastService.success).toHaveBeenCalledWith('Recovery email sent');
    expect(component.form.value.email).toBeNull();
  });

  it('should show error toast on failure', () => {
    userService.requestPasswordReset.and.returnValue(throwError(() => ({ error: { message: 'Server error' } })));
    component.form.setValue({ email: 'user@example.com' });

    component.onSubmit();

    expect(toastService.error).toHaveBeenCalledWith('Server error');
    expect(component.isLoading).toBeFalse();
  });

  it('should not submit invalid email', () => {
    component.form.setValue({ email: 'not-an-email' });
    component.onSubmit();
    expect(userService.requestPasswordReset).not.toHaveBeenCalled();
  });

  it('should navigate back to sign-in', () => {
    component.backButton.onClick?.();
    expect(router.navigate).toHaveBeenCalledWith(['/sign-in']);
  });
});
