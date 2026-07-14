import { routes } from './app.routes';
import { ProductLandingComponent } from './pages/product-landing/product-landing.component';
import { SignInComponent } from './pages/sign-in/sign-in.component';
import { SignUpComponent } from './pages/sign-up/sign-up.component';
import { ForgotPasswordComponent } from './pages/forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './pages/reset-password/reset-password.component';
import { CrewHomeComponent } from './pages/crew-home/crew-home.component';
import { FriendsComponent } from './pages/friends/friends.component';
import { ProfileComponent } from './pages/profile/profile.component';
import { UserHomeComponent } from './pages/user-home/user-home.component';
import { CreateCrewComponent } from './pages/create-crew/create-crew.component';
import { JoinCrewComponent } from './pages/join-crew/join-crew.component';
import { GiftLogComponent } from './pages/gift-log/gift-log.component';
import { RecordGiftComponent } from './pages/record-gift/record-gift.component';
import { ProfileSettingsComponent } from './pages/profile-settings/profile-settings.component';
import { ActivityCenterComponent } from './pages/activity-center/activity-center.component';
import { EditCrewComponent } from './pages/edit-crew/edit-crew.component';
import { ChatListComponent } from './pages/chats/chat-list/chat-list.component';
import { ChatCreateComponent } from './pages/chats/chat-create/chat-create.component';
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
    expect(routes.find(r => r.path === 'sign-in-success')).toBeUndefined();
  });

  it('should define authenticated app routes with authGuard', () => {
    const appRoutes = ['app/crew', 'app/friends', 'app/profile', 'app/profile/user', 'app/profile/activity', 'app/profile/preferences', 'app/crew/create', 'app/crew/join'];

    for (const path of appRoutes) {
      const route = routes.find(r => r.path === path);
      expect(route?.canActivate).toContain(authGuard);
    }

    expect(routes.find(r => r.path === 'app/crew')?.component).toBe(CrewHomeComponent);
    expect(routes.find(r => r.path === 'app/friends')?.component).toBe(FriendsComponent);
    expect(routes.find(r => r.path === 'app/profile')?.component).toBe(UserHomeComponent);
    expect(routes.find(r => r.path === 'app/profile/user')?.component).toBe(ProfileComponent);
    expect(routes.find(r => r.path === 'app/profile/activity')?.component).toBe(ActivityCenterComponent);
    expect(routes.find(r => r.path === 'app/profile/preferences')?.component).toBe(ProfileSettingsComponent);
    expect(routes.find(r => r.path === 'app/crew/create')?.component).toBe(CreateCrewComponent);
    expect(routes.find(r => r.path === 'app/crew/join')?.component).toBe(JoinCrewComponent);
    expect(routes.find(r => r.path === 'app/crew/gift-log')?.component).toBe(GiftLogComponent);
    expect(routes.find(r => r.path === 'app/crew/gift-log/record')?.component).toBe(RecordGiftComponent);
    expect(routes.find(r => r.path === 'app/crew/edit')?.component).toBe(EditCrewComponent);
    expect(routes.find(r => r.path === 'app/crew/chats')?.component).toBe(ChatListComponent);
    expect(routes.find(r => r.path === 'app/crew/chats/create')?.component).toBe(ChatCreateComponent);
    expect(routes.find(r => r.path === 'app/crew')?.pathMatch).toBe('full');
    expect(routes.find(r => r.path === 'app/crew/chats')?.pathMatch).toBe('full');
    expect(routes.find(r => r.path === 'app/profile/preferences/placeholder')).toBeUndefined();
  });

  it('should redirect unknown paths to root', () => {
    const wildcard = routes.find(r => r.path === '**');
    expect(wildcard?.redirectTo).toBe('');
  });
});
