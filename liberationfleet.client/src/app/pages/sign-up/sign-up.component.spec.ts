import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { SignUpComponent } from './sign-up.component';
import { AuthService } from '../../services/auth.service';
import { UserService } from '../../services/user.service';
import { ToastService } from '../../components/toast/toast.component';
import { createAuthServiceMock, createUserServiceMock, createToastServiceMock, validSignUpPassword } from '../../testing/test-helpers';

describe('SignUpComponent', () => {
  let fixture: ComponentFixture<SignUpComponent>;
  let component: SignUpComponent;
  let authService: jasmine.SpyObj<AuthService>;
  let userService: jasmine.SpyObj<UserService>;
  let toastService: jasmine.SpyObj<ToastService>;
  let router: Router;

  beforeEach(async () => {
    authService = createAuthServiceMock();
    userService = createUserServiceMock();
    toastService = createToastServiceMock();

    await TestBed.configureTestingModule({
      imports: [SignUpComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authService },
        { provide: UserService, useValue: userService },
        { provide: ToastService, useValue: toastService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(SignUpComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  function fillValidForm(): void {
    component.form.setValue({
      username: 'newuser',
      email: 'new@example.com',
      password: validSignUpPassword,
      confirmPassword: validSignUpPassword,
      privacyPolicyAccepted: true
    });
  }

  it('should create with invalid form initially', () => {
    expect(component.form.invalid).toBeTrue();
    expect(component.signUpButton.disabled).toBeTrue();
  });

  it('hasPasswordRequirement should validate password rules', () => {
    component.form.get('password')?.setValue(validSignUpPassword);

    expect(component.hasPasswordRequirement('uppercase')).toBeTrue();
    expect(component.hasPasswordRequirement('lowercase')).toBeTrue();
    expect(component.hasPasswordRequirement('number')).toBeTrue();
    expect(component.hasPasswordRequirement('special')).toBeTrue();
    expect(component.hasPasswordRequirement('length')).toBeTrue();
  });

  it('should reject weak passwords', () => {
    component.form.patchValue({
      username: 'user',
      email: 'user@example.com',
      password: 'weak',
      confirmPassword: 'weak',
      privacyPolicyAccepted: true
    });

    expect(component.form.invalid).toBeTrue();
    expect(component.form.get('password')?.hasError('passwordStrength')).toBeTrue();
  });

  it('should reject mismatched passwords', () => {
    component.form.patchValue({
      username: 'user',
      email: 'user@example.com',
      password: validSignUpPassword,
      confirmPassword: 'Password2!',
      privacyPolicyAccepted: true
    });

    expect(component.form.hasError('passwordMismatch')).toBeTrue();
  });

  it('should register and navigate on success', () => {
    const response = { success: true, token: 'jwt', user: { id: 1, username: 'newuser', email: 'new@example.com' } };
    userService.create.and.returnValue(of(response));
    fillValidForm();

    component.onSubmit();

    expect(userService.create).toHaveBeenCalledWith({
      username: 'newuser',
      email: 'new@example.com',
      password: validSignUpPassword,
      confirmPassword: validSignUpPassword
    });
    expect(authService.establishSession).toHaveBeenCalledWith(response);
    expect(toastService.success).toHaveBeenCalledWith('Account created successfully!');
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew']);
  });

  it('should show error toast on registration failure', () => {
    userService.create.and.returnValue(throwError(() => ({ error: { message: 'Email already registered' } })));
    fillValidForm();

    component.onSubmit();

    expect(toastService.error).toHaveBeenCalledWith('Email already registered');
    expect(component.isLoading).toBeFalse();
  });

  it('showPrivacyPolicy should prevent default and show alert', () => {
    const event = new Event('click');
    spyOn(event, 'preventDefault');
    spyOn(window, 'alert');

    component.showPrivacyPolicy(event);

    expect(event.preventDefault).toHaveBeenCalled();
    expect(window.alert).toHaveBeenCalled();
  });

  it('should navigate back to sign-in', () => {
    component.backButton.onClick?.();
    expect(router.navigate).toHaveBeenCalledWith(['/sign-in']);
  });
});
