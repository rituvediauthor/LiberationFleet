import { routes } from './app.routes';
import { ProductLandingComponent } from './pages/product-landing/product-landing.component';
import { SignInComponent } from './pages/sign-in/sign-in.component';
import { SignUpComponent } from './pages/sign-up/sign-up.component';
import { ForgotPasswordComponent } from './pages/forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './pages/reset-password/reset-password.component';
import { SignInSuccessComponent } from './pages/sign-in-success/sign-in-success.component';
import { CrewHomeComponent } from './pages/crew-home/crew-home.component';
import { FriendsComponent } from './pages/friends/friends.component';
import { ProfileComponent } from './pages/profile/profile.component';
import { CreateCrewComponent } from './pages/create-crew/create-crew.component';
import { JoinCrewComponent } from './pages/join-crew/join-crew.component';
import { authGuard } from './guards/auth.guard';

describe('app.routes', () => {
  it('should define landing route at root', () => {
    const route = routes.find(r => r.path === '');
    expect(route?.component).toBe(ProductLandingComponent);
  });

  it('should define auth-related routes', () => {
    expect(routes.find(r => r.path === 'sign-in')?.component).toBe(SignInComponent);
    expect(routes.find(r => r.path === 'sign-up')?.component).toBe(SignUpComponent);
    expect(routes.find(r => r.path === 'forgot-password')?.component).toBe(ForgotPasswordComponent);
    expect(routes.find(r => r.path === 'reset-password')?.component).toBe(ResetPasswordComponent);
    expect(routes.find(r => r.path === 'sign-in-success')?.component).toBe(SignInSuccessComponent);
  });

  it('should define authenticated app routes with authGuard', () => {
    const appRoutes = ['app/crew', 'app/friends', 'app/profile', 'app/crew/create', 'app/crew/join'];

    for (const path of appRoutes) {
      const route = routes.find(r => r.path === path);
      expect(route?.canActivate).toContain(authGuard);
    }

    expect(routes.find(r => r.path === 'app/crew')?.component).toBe(CrewHomeComponent);
    expect(routes.find(r => r.path === 'app/friends')?.component).toBe(FriendsComponent);
    expect(routes.find(r => r.path === 'app/profile')?.component).toBe(ProfileComponent);
    expect(routes.find(r => r.path === 'app/crew/create')?.component).toBe(CreateCrewComponent);
    expect(routes.find(r => r.path === 'app/crew/join')?.component).toBe(JoinCrewComponent);
  });

  it('should redirect unknown paths to root', () => {
    const wildcard = routes.find(r => r.path === '**');
    expect(wildcard?.redirectTo).toBe('');
  });
});
