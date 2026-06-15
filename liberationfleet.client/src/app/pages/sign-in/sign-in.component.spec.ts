import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { SignInComponent } from './sign-in.component';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../components/toast/toast.component';
import { createAuthServiceMock, createToastServiceMock } from '../../testing/test-helpers';

describe('SignInComponent', () => {
  let fixture: ComponentFixture<SignInComponent>;
  let component: SignInComponent;
  let authService: jasmine.SpyObj<AuthService>;
  let toastService: jasmine.SpyObj<ToastService>;
  let router: Router;

  beforeEach(async () => {
    authService = createAuthServiceMock();
    toastService = createToastServiceMock();

    await TestBed.configureTestingModule({
      imports: [SignInComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authService },
        { provide: ToastService, useValue: toastService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(SignInComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create with empty required fields', () => {
    expect(component.form.invalid).toBeTrue();
  });

  it('should not submit when form is invalid', () => {
    component.onSubmit();
    expect(authService.login).not.toHaveBeenCalled();
  });

  it('should login and navigate on success', () => {
    authService.login.and.returnValue(of({
      success: true,
      token: 'jwt',
      user: { id: 1, username: 'user', email: 'user@example.com' }
    }));

    component.form.setValue({ usernameOrEmail: 'user@example.com', password: 'password123' });
    component.onSubmit();

    expect(authService.login).toHaveBeenCalledWith({
      usernameOrEmail: 'user@example.com',
      password: 'password123'
    });
    expect(toastService.success).toHaveBeenCalledWith('Sign in successful!');
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew']);
  });

  it('should show error toast on login failure', () => {
    authService.login.and.returnValue(throwError(() => ({ error: { message: 'Invalid credentials' } })));

    component.form.setValue({ usernameOrEmail: 'user@example.com', password: 'wrong' });
    component.onSubmit();

    expect(toastService.error).toHaveBeenCalledWith('Invalid credentials');
    expect(component.isLoading).toBeFalse();
    expect(component.signInButton.disabled).toBeFalse();
  });

  it('should navigate back to home when back button is clicked', () => {
    component.backButton.onClick?.();
    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  it('should show validation errors when fields are touched', () => {
    component.form.get('usernameOrEmail')?.markAsTouched();
    component.form.get('password')?.markAsTouched();
    fixture.detectChanges();

    const errors = fixture.nativeElement.querySelectorAll('.error-text');
    expect(errors.length).toBe(2);
  });
});
