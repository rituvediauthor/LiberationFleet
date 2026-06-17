import { Routes } from '@angular/router';
import { ProductLandingComponent } from './pages/product-landing/product-landing.component';
import { SignInComponent } from './pages/sign-in/sign-in.component';
import { SignUpComponent } from './pages/sign-up/sign-up.component';
import { ForgotPasswordComponent } from './pages/forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './pages/reset-password/reset-password.component';
import { SignInSuccessComponent } from './pages/sign-in-success/sign-in-success.component';
import { CrewHomeComponent } from './pages/crew-home/crew-home.component';
import { FriendsComponent } from './pages/friends/friends.component';
import { ProfileComponent } from './pages/profile/profile.component';
import { UserHomeComponent } from './pages/user-home/user-home.component';
import { CreateCrewComponent } from './pages/create-crew/create-crew.component';
import { JoinCrewComponent } from './pages/join-crew/join-crew.component';
import { GiftLogComponent } from './pages/gift-log/gift-log.component';
import { RecordGiftComponent } from './pages/record-gift/record-gift.component';
import { CompleteGiftComponent } from './pages/complete-gift/complete-gift.component';
import { PlaceholderPageComponent } from './pages/placeholder/placeholder-page.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    component: ProductLandingComponent
  },
  {
    path: 'sign-in',
    component: SignInComponent
  },
  {
    path: 'sign-up',
    component: SignUpComponent
  },
  {
    path: 'forgot-password',
    component: ForgotPasswordComponent
  },
  {
    path: 'reset-password',
    component: ResetPasswordComponent
  },
  {
    path: 'sign-in-success',
    component: SignInSuccessComponent
  },
  {
    path: 'app/crew',
    component: CrewHomeComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/friends',
    component: FriendsComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/profile',
    component: UserHomeComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/profile/user',
    component: ProfileComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/profile/activity',
    component: PlaceholderPageComponent,
    canActivate: [authGuard],
    data: { title: 'Activity center', backTo: '/app/profile' }
  },
  {
    path: 'app/profile/preferences',
    component: PlaceholderPageComponent,
    canActivate: [authGuard],
    data: { title: 'Preferences', backTo: '/app/profile' }
  },
  {
    path: 'app/crew/create',
    component: CreateCrewComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/join',
    component: JoinCrewComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/edit',
    component: PlaceholderPageComponent,
    canActivate: [authGuard],
    data: { title: 'Edit Crew', backTo: '/app/crew' }
  },
  {
    path: 'app/crew/gift-log',
    component: GiftLogComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/gift-log/record',
    component: RecordGiftComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/gift-log/complete',
    component: CompleteGiftComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/chats',
    component: PlaceholderPageComponent,
    canActivate: [authGuard],
    data: { title: 'Chats', backTo: '/app/crew' }
  },
  {
    path: 'app/crew/proposals',
    component: PlaceholderPageComponent,
    canActivate: [authGuard],
    data: { title: 'Proposals', backTo: '/app/crew' }
  },
  {
    path: 'app/crew/projects',
    component: PlaceholderPageComponent,
    canActivate: [authGuard],
    data: { title: 'Projects', backTo: '/app/crew' }
  },
  {
    path: '**',
    redirectTo: ''
  }
];
