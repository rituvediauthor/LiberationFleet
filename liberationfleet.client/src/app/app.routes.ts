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
import { CreateCrewComponent } from './pages/create-crew/create-crew.component';
import { JoinCrewComponent } from './pages/join-crew/join-crew.component';
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
    component: ProfileComponent,
    canActivate: [authGuard]
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
    path: '**',
    redirectTo: ''
  }
];
