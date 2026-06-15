import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { SignInSuccessComponent } from './sign-in-success.component';
import { AuthService } from '../../services/auth.service';
import { createAuthServiceMock } from '../../testing/test-helpers';

describe('SignInSuccessComponent', () => {
  let fixture: ComponentFixture<SignInSuccessComponent>;
  let component: SignInSuccessComponent;
  let authService: jasmine.SpyObj<AuthService>;
  let currentUser$: BehaviorSubject<{ username: string; email: string } | null>;
  let router: Router;

  beforeEach(async () => {
    currentUser$ = new BehaviorSubject<{ username: string; email: string } | null>({
      username: 'fleetuser',
      email: 'fleet@example.com'
    });

    authService = createAuthServiceMock();
    Object.defineProperty(authService, 'currentUser$', { get: () => currentUser$.asObservable() });

    await TestBed.configureTestingModule({
      imports: [SignInSuccessComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(SignInSuccessComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and display current user info', () => {
    expect(component.currentUser?.username).toBe('fleetuser');
    expect(fixture.nativeElement.textContent).toContain('fleetuser');
    expect(fixture.nativeElement.textContent).toContain('fleet@example.com');
  });

  it('should logout and navigate home', () => {
    component.logoutButton.onClick?.();

    expect(authService.logout).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  it('should update displayed user when currentUser$ emits', () => {
    currentUser$.next({ username: 'updated', email: 'updated@example.com' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('updated');
    expect(fixture.nativeElement.textContent).toContain('updated@example.com');
  });
});
