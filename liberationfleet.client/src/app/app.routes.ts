import { Routes } from '@angular/router';
import { ProductLandingComponent } from './pages/product-landing/product-landing.component';
import { SignInComponent } from './pages/sign-in/sign-in.component';
import { SignUpComponent } from './pages/sign-up/sign-up.component';
import { ForgotPasswordComponent } from './pages/forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './pages/reset-password/reset-password.component';
import { SignInSuccessComponent } from './pages/sign-in-success/sign-in-success.component';
import { CrewHomeComponent } from './pages/crew-home/crew-home.component';
import { FriendsComponent } from './pages/friends/friends.component';
import { FriendRequestsComponent } from './pages/friends/friend-requests/friend-requests.component';
import { FriendBlockedComponent } from './pages/friends/friend-blocked/friend-blocked.component';
import { FindFriendComponent } from './pages/friends/find-friend/find-friend.component';
import { FriendDmComponent } from './pages/friends/friend-dm/friend-dm.component';
import { ProfileComponent } from './pages/profile/profile.component';
import { GiftHistoryListComponent } from './pages/profile/gift-history-list/gift-history-list.component';
import { GiftHistoryDetailComponent } from './pages/profile/gift-history-detail/gift-history-detail.component';
import { UserHomeComponent } from './pages/user-home/user-home.component';
import { CreateCrewComponent } from './pages/create-crew/create-crew.component';
import { JoinCrewComponent } from './pages/join-crew/join-crew.component';
import { MyJoinRequestsComponent } from './pages/my-join-requests/my-join-requests.component';
import { GiftLogComponent } from './pages/gift-log/gift-log.component';
import { RecordGiftComponent } from './pages/record-gift/record-gift.component';
import { AddNonCrewmateComponent } from './pages/record-gift/add-non-crewmate/add-non-crewmate.component';
import { SeasonSetupComponent } from './pages/season-setup/season-setup.component';
import { JoinSeasonComponent } from './pages/join-season/join-season.component';
import { NotificationsComponent } from './pages/notifications/notifications.component';
import { ProfileSettingsComponent } from './pages/profile-settings/profile-settings.component';
import { NotificationSettingsComponent } from './pages/notification-settings/notification-settings.component';
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
import { ChatEditComponent } from './pages/chats/chat-edit/chat-edit.component';
import { ChatTextComponent } from './pages/chats/chat-text/chat-text.component';
import { ChatVoiceComponent } from './pages/chats/chat-voice/chat-voice.component';
import { RuleListComponent } from './pages/rules/rule-list/rule-list.component';
import { RuleCreateComponent } from './pages/rules/rule-create/rule-create.component';
import { RuleEditComponent } from './pages/rules/rule-edit/rule-edit.component';
import { EditCrewComponent } from './pages/edit-crew/edit-crew.component';
import { CrewmateListComponent } from './pages/crewmates/crewmate-list/crewmate-list.component';
import { KickedCrewmatesListComponent } from './pages/crewmates/kicked-crewmates-list/kicked-crewmates-list.component';
import { CrewmateDetailComponent } from './pages/crewmates/crewmate-detail/crewmate-detail.component';
import { NominateRolesComponent } from './pages/crewmates/nominate-roles/nominate-roles.component';
import { LibraryHubComponent } from './pages/library/library-hub/library-hub.component';
import { LibraryUnlockComponent } from './pages/library/library-unlock/library-unlock.component';
import { LibraryDurableListComponent } from './pages/library/library-durable-list/library-durable-list.component';
import { CreateLibraryOfferingComponent } from './pages/library/create-library-offering/create-library-offering.component';
import { EditLibraryOfferingComponent } from './pages/library/edit-library-offering/edit-library-offering.component';
import { LibraryUnitDetailComponent } from './pages/library/library-unit-detail/library-unit-detail.component';
import { LibraryMyRequestsComponent } from './pages/library/library-my-requests/library-my-requests.component';
import { LibraryRequestDetailComponent } from './pages/library/library-request-detail/library-request-detail.component';
import { LibraryIncomingRequestsComponent } from './pages/library/library-incoming-requests/library-incoming-requests.component';
import { LibraryRequestChatComponent } from './pages/library/library-request-chat/library-request-chat.component';
import { LibraryUnitActiveRequestsComponent } from './pages/library/library-unit-active-requests/library-unit-active-requests.component';
import { LibraryStockListComponent } from './pages/library/library-stock-list/library-stock-list.component';
import { LibraryMyOfferingsComponent } from './pages/library/library-my-offerings/library-my-offerings.component';
import { authGuard } from './guards/auth.guard';
import { libraryAccessGuard } from './guards/library-access.guard';

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
    pathMatch: 'full',
    canActivate: [authGuard]
  },
  {
    path: 'app/friends',
    component: FriendsComponent,
    pathMatch: 'full',
    canActivate: [authGuard]
  },
  {
    path: 'app/friends/requests',
    component: FriendRequestsComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/friends/blocked',
    component: FriendBlockedComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/friends/find',
    component: FindFriendComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/friends/messages/:userId',
    component: FriendDmComponent,
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
    pathMatch: 'full',
    canActivate: [authGuard]
  },
  {
    path: 'app/profile/user',
    component: ProfileComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/profile/gift-history',
    component: GiftHistoryListComponent,
    pathMatch: 'full',
    canActivate: [authGuard]
  },
  {
    path: 'app/profile/gift-history/:userId',
    component: GiftHistoryDetailComponent,
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
    component: ProfileSettingsComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/profile/preferences/notifications',
    component: NotificationSettingsComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/profile/preferences/placeholder',
    component: PlaceholderPageComponent,
    canActivate: [authGuard],
    data: { title: 'Preferences', backTo: '/app/profile/preferences' }
  },
  {
    path: 'app/profile/settings',
    redirectTo: 'app/profile/preferences',
    pathMatch: 'full'
  },
  {
    path: 'app/profile/settings/notifications',
    redirectTo: 'app/profile/preferences/notifications',
    pathMatch: 'full'
  },
  {
    path: 'app/profile/settings/placeholder',
    redirectTo: 'app/profile/preferences/placeholder',
    pathMatch: 'full'
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
    path: 'app/crew/join-requests',
    component: MyJoinRequestsComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/edit',
    component: EditCrewComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/gift-log',
    component: GiftLogComponent,
    pathMatch: 'full',
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
    path: 'app/crew/gift-log/record/add-non-crewmate',
    component: AddNonCrewmateComponent,
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
    pathMatch: 'full',
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/chats/create',
    component: ChatCreateComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/chats/:id/edit',
    component: ChatEditComponent,
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
    pathMatch: 'full',
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
    pathMatch: 'full',
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
    pathMatch: 'full',
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
    component: CrewmateListComponent,
    pathMatch: 'full',
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/crewmates/kicked',
    component: KickedCrewmatesListComponent,
    pathMatch: 'full',
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/crewmates/:id/nominate-roles',
    component: NominateRolesComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/crewmates/:id',
    component: CrewmateDetailComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/rules',
    component: RuleListComponent,
    pathMatch: 'full',
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/rules/create',
    component: RuleCreateComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/rules/:id/edit',
    component: RuleEditComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/library-of-things/unlock',
    component: LibraryUnlockComponent,
    canActivate: [authGuard]
  },
  {
    path: 'app/crew/library-of-things',
    component: LibraryHubComponent,
    canActivate: [authGuard, libraryAccessGuard]
  },
  {
    path: 'app/crew/library-of-things/durable',
    component: LibraryDurableListComponent,
    canActivate: [authGuard, libraryAccessGuard]
  },
  {
    path: 'app/crew/library-of-things/requests/mine',
    component: LibraryMyRequestsComponent,
    canActivate: [authGuard, libraryAccessGuard]
  },
  {
    path: 'app/crew/library-of-things/requests/:id/chat',
    component: LibraryRequestChatComponent,
    canActivate: [authGuard, libraryAccessGuard]
  },
  {
    path: 'app/crew/library-of-things/requests/:id',
    component: LibraryRequestDetailComponent,
    canActivate: [authGuard, libraryAccessGuard]
  },
  {
    path: 'app/crew/library-of-things/requests',
    component: LibraryIncomingRequestsComponent,
    canActivate: [authGuard, libraryAccessGuard]
  },
  {
    path: 'app/crew/library-of-things/units/:unitId/active-requests',
    component: LibraryUnitActiveRequestsComponent,
    canActivate: [authGuard, libraryAccessGuard]
  },
  {
    path: 'app/crew/library-of-things/consumable',
    component: LibraryStockListComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { title: 'Consumable Goods', stockKind: 'Consumable' }
  },
  {
    path: 'app/crew/library-of-things/services',
    component: LibraryStockListComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { title: 'Services', stockKind: 'Service' }
  },
  {
    path: 'app/crew/library-of-things/mine',
    component: LibraryMyOfferingsComponent,
    canActivate: [authGuard, libraryAccessGuard]
  },
  {
    path: 'app/crew/library-of-things/offerings/create',
    component: CreateLibraryOfferingComponent,
    canActivate: [authGuard, libraryAccessGuard]
  },
  {
    path: 'app/crew/library-of-things/offerings/:id/edit',
    component: EditLibraryOfferingComponent,
    canActivate: [authGuard, libraryAccessGuard]
  },
  {
    path: 'app/crew/library-of-things/units/:id',
    component: LibraryUnitDetailComponent,
    canActivate: [authGuard, libraryAccessGuard]
  },
  {
    path: '**',
    redirectTo: ''
  }
];
