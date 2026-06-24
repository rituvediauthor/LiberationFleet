import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
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
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    authService = createAuthServiceMock();
    userService = createUserServiceMock();
    toastService = createToastServiceMock();

    await TestBed.configureTestingModule({
      imports: [SignUpComponent, HttpClientTestingModule],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authService },
        { provide: UserService, useValue: userService },
        { provide: ToastService, useValue: toastService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    httpMock = TestBed.inject(HttpTestingController);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(SignUpComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
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

  it('should register, show recovery key, then navigate on confirm', async () => {
    const response = { success: true, token: 'jwt', user: { id: 1, username: 'newuser', email: 'new@example.com' } };
    userService.create.and.returnValue(of(response));
    authService.setupNewAccountEncryption.and.returnValue(Promise.resolve());
    fillValidForm();

    component.onSubmit();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(userService.create).toHaveBeenCalledWith({
      username: 'newuser',
      email: 'new@example.com',
      password: validSignUpPassword,
      confirmPassword: validSignUpPassword
    });
    expect(authService.establishSession).toHaveBeenCalledWith(response);
    expect(component.showRecoveryKeyModal).toBeTrue();
    expect(component.pendingRecoveryPhrase.split(' ').length).toBe(12);
    expect(router.navigate).not.toHaveBeenCalled();

    await component.onRecoveryKeyConfirmed();
    fixture.detectChanges();

    expect(authService.setupNewAccountEncryption).toHaveBeenCalled();
    expect(toastService.success).toHaveBeenCalledWith('Account created successfully!');
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew']);
  });

  it('should show error toast on registration failure', async () => {
    userService.create.and.returnValue(throwError(() => ({ error: { message: 'Email already registered' } })));
    fillValidForm();

    component.onSubmit();
    await fixture.whenStable();

    expect(toastService.error).toHaveBeenCalledWith('Email already registered');
    expect(component.isLoading).toBeFalse();
  });

  it('showPrivacyPolicy should open modal and load policy text', async () => {
    const event = new Event('click');
    spyOn(event, 'preventDefault');

    component.showPrivacyPolicy(event);

    expect(event.preventDefault).toHaveBeenCalled();
    expect(component.showPrivacyPolicyModal).toBeTrue();

    const req = httpMock.expectOne('/assets/privacy-policy.txt');
    expect(req.request.method).toBe('GET');
    req.flush('Privacy policy content');

    await fixture.whenStable();
    expect(component.privacyPolicyText).toBe('Privacy policy content');
  });

  it('should navigate back to sign-in', () => {
    component.backButton.onClick?.();
    expect(router.navigate).toHaveBeenCalledWith(['/sign-in']);
  });
});
