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
import { SeasonSetupComponent } from './pages/season-setup/season-setup.component';
import { JoinSeasonComponent } from './pages/join-season/join-season.component';
import { NotificationsComponent } from './pages/notifications/notifications.component';
import { PlaceholderPageComponent } from './pages/placeholder/placeholder-page.component';
import { ProposalsTypeComponent } from './pages/proposals/proposals-type/proposals-type.component';
import { ProposalsListComponent } from './pages/proposals/proposals-list/proposals-list.component';
import { CreateProposalComponent } from './pages/proposals/create-proposal/create-proposal.component';
import { ProposalDetailComponent } from './pages/proposals/proposal-detail/proposal-detail.component';
import { DiscussionListComponent } from './pages/crew-discussion/discussion-list/discussion-list.component';
import { DiscussionCreateComponent } from './pages/crew-discussion/discussion-create/discussion-create.component';
import { DiscussionDetailComponent } from './pages/crew-discussion/discussion-detail/discussion-detail.component';
import { ChatListComponent } from './pages/chats/chat-list/chat-list.component';
import { ChatCreateComponent } from './pages/chats/chat-create/chat-create.component';
import { ChatTextComponent } from './pages/chats/chat-text/chat-text.component';
import { ChatVoiceComponent } from './pages/chats/chat-voice/chat-voice.component';
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
    path: 'app/notifications',
    component: NotificationsComponent,
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
    path: 'app/crew/season-setup',
    component: SeasonSetupComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/join-season',
    component: JoinSeasonComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/gift-log/record',
    component: RecordGiftComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/chats',
    component: ChatListComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/chats/create',
    component: ChatCreateComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/chats/:id/voice',
    component: ChatVoiceComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/chats/:id',
    component: ChatTextComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/proposals',
    component: ProposalsTypeComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/proposals/list/:status',
    component: ProposalsListComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/proposals/create',
    component: CreateProposalComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/proposals/:id',
    component: ProposalDetailComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/projects',
    component: DiscussionListComponent,
    canActivate: [authGuard],
    data: { discussionKind: 'projects' }
  },
  {
    path: 'app/crew/projects/create',
    component: DiscussionCreateComponent,
    canActivate: [authGuard],
    data: { discussionKind: 'projects' }
  },
  {
    path: 'app/crew/projects/:id',
    component: DiscussionDetailComponent,
    canActivate: [authGuard],
    data: { discussionKind: 'projects' }
  },
  {
    path: 'app/crew/forums',
    component: DiscussionListComponent,
    canActivate: [authGuard],
    data: { discussionKind: 'forums' }
  },
  {
    path: 'app/crew/forums/create',
    component: DiscussionCreateComponent,
    canActivate: [authGuard],
    data: { discussionKind: 'forums' }
  },
  {
    path: 'app/crew/forums/:id',
    component: DiscussionDetailComponent,
    canActivate: [authGuard],
    data: { discussionKind: 'forums' }
  },
  {
    path: 'app/crew/crewmates',
    component: PlaceholderPageComponent,
    canActivate: [authGuard],
    data: { title: 'Crewmates', backTo: '/app/crew' }
  },
  {
    path: 'app/crew/rules',
    component: PlaceholderPageComponent,
    canActivate: [authGuard],
    data: { title: 'Rules', backTo: '/app/crew' }
  },
  {
    path: 'app/crew/library-of-things',
    component: PlaceholderPageComponent,
    canActivate: [authGuard],
    data: { title: 'Library of Things', backTo: '/app/crew' }
  },
  {
    path: '**',
    redirectTo: ''
  }
];
