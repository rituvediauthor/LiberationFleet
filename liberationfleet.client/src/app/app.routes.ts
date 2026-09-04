import { Routes } from '@angular/router';
import { ProductLandingComponent } from './pages/product-landing/product-landing.component';
import { SignInComponent } from './pages/sign-in/sign-in.component';
import { SignUpComponent } from './pages/sign-up/sign-up.component';
import { ForgotPasswordComponent } from './pages/forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './pages/reset-password/reset-password.component';
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
import { MyInvitationsComponent } from './pages/my-invitations/my-invitations.component';
import { GiftLogComponent } from './pages/gift-log/gift-log.component';
import { GiftLogDetailComponent } from './pages/gift-log/gift-log-detail/gift-log-detail.component';
import { SeasonInfoComponent } from './pages/gift-log/season-info/season-info.component';
import { RecordGiftComponent } from './pages/record-gift/record-gift.component';
import { AddNonCrewmateComponent } from './pages/record-gift/add-non-crewmate/add-non-crewmate.component';
import { EmergencyRequestsListComponent } from './pages/emergency-requests/emergency-requests-list/emergency-requests-list.component';
import { EmergencyRequestCreateComponent } from './pages/emergency-requests/emergency-request-create/emergency-request-create.component';
import { EmergencyRequestDetailComponent } from './pages/emergency-requests/emergency-request-detail/emergency-request-detail.component';
import { SeasonSetupComponent } from './pages/season-setup/season-setup.component';
import { JoinSeasonComponent } from './pages/join-season/join-season.component';
import { NotificationsComponent } from './pages/notifications/notifications.component';
import { ProfileSettingsComponent } from './pages/profile-settings/profile-settings.component';
import { NotificationSettingsComponent } from './pages/notification-settings/notification-settings.component';
import { ContentSettingsComponent } from './pages/content-settings/content-settings.component';
import { VoiceSettingsComponent } from './pages/voice-settings/voice-settings.component';
import { ThemeSettingsComponent } from './pages/theme-settings/theme-settings.component';
import { SecuritySettingsComponent } from './pages/security-settings/security-settings.component';
import { SecurityAlertsComponent } from './pages/security-alerts/security-alerts.component';
import { PasswordUpdateComponent } from './pages/password-update/password-update.component';
import { ActivityCenterComponent } from './pages/activity-center/activity-center.component';
import { DonateComponent } from './pages/donate/donate.component';
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
import { ArrangeChatChannelsComponent } from './pages/chats/arrange-chat-channels/arrange-chat-channels.component';
import { RuleListComponent } from './pages/rules/rule-list/rule-list.component';
import { RuleCreateComponent } from './pages/rules/rule-create/rule-create.component';
import { RuleEditComponent } from './pages/rules/rule-edit/rule-edit.component';
import { EditCrewComponent } from './pages/edit-crew/edit-crew.component';
import { CrewmateListComponent } from './pages/crewmates/crewmate-list/crewmate-list.component';
import { InviteCrewmateComponent } from './pages/crewmates/invite-crewmate/invite-crewmate.component';
import { CrewInvitationComponent } from './pages/crewmates/crew-invitation/crew-invitation.component';
import { KickedCrewmatesListComponent } from './pages/crewmates/kicked-crewmates-list/kicked-crewmates-list.component';
import { CrewmateDetailComponent } from './pages/crewmates/crewmate-detail/crewmate-detail.component';
import { NominateRolesComponent } from './pages/crewmates/nominate-roles/nominate-roles.component';
import { LibraryHubComponent } from './pages/library/library-hub/library-hub.component';
import { LibraryUnlockComponent } from './pages/library/library-unlock/library-unlock.component';
import { LibraryDurableListComponent } from './pages/library/library-durable-list/library-durable-list.component';
import { LibraryOfferingUnitsComponent } from './pages/library/library-offering-units/library-offering-units.component';
import { CreateLibraryOfferingComponent } from './pages/library/create-library-offering/create-library-offering.component';
import { EditLibraryOfferingComponent } from './pages/library/edit-library-offering/edit-library-offering.component';
import { LibraryUnitDetailComponent } from './pages/library/library-unit-detail/library-unit-detail.component';
import { LibraryMyRequestsComponent } from './pages/library/library-my-requests/library-my-requests.component';
import { LibraryMyRequestStatusComponent } from './pages/library/library-my-request-status/library-my-request-status.component';
import { LibraryDeniedRequestsComponent } from './pages/library/library-denied-requests/library-denied-requests.component';
import { LibraryRequestDetailComponent } from './pages/library/library-request-detail/library-request-detail.component';
import { LibraryIncomingRequestsComponent } from './pages/library/library-incoming-requests/library-incoming-requests.component';
import { LibraryRequestChatComponent } from './pages/library/library-request-chat/library-request-chat.component';
import { LibraryUnitActiveRequestsComponent } from './pages/library/library-unit-active-requests/library-unit-active-requests.component';
import { LibraryStockListComponent } from './pages/library/library-stock-list/library-stock-list.component';
import { LibraryMyOfferingsComponent } from './pages/library/library-my-offerings/library-my-offerings.component';
import { LibraryTaskBoardComponent } from './pages/library/library-tasks/library-task-board/library-task-board.component';
import { LibraryTaskFormComponent } from './pages/library/library-tasks/library-task-form/library-task-form.component';
import { LibraryTaskDetailComponent } from './pages/library/library-tasks/library-task-detail/library-task-detail.component';
import { LibraryTaskNoDeadlineComponent } from './pages/library/library-tasks/library-task-no-deadline/library-task-no-deadline.component';
import { authGuard } from './guards/auth.guard';
import { libraryAccessGuard } from './guards/library-access.guard';
import { fleetRulesAcceptedGuard } from './guards/fleet-rules-accepted.guard';
import { FleetHomeComponent } from './pages/fleet/fleet-home/fleet-home.component';
import { CreateFleetComponent } from './pages/fleet/create-fleet/create-fleet.component';
import { JoinFleetComponent } from './pages/fleet/join-fleet/join-fleet.component';
import { InviteCrewComponent } from './pages/fleet/invite-crew/invite-crew.component';
import { AcceptFleetRulesComponent } from './pages/fleet/accept-fleet-rules/accept-fleet-rules.component';
import { FleetJoinRequestsComponent } from './pages/fleet/fleet-join-requests/fleet-join-requests.component';
import { EditFleetComponent } from './pages/fleet/edit-fleet/edit-fleet.component';
import { FleetCrewsComponent } from './pages/fleet/fleet-crews/fleet-crews.component';
import { FleetCrewDetailComponent } from './pages/fleet/fleet-crew-detail/fleet-crew-detail.component';
import { FleetCrewmateDetailComponent } from './pages/fleet/fleet-crewmate-detail/fleet-crewmate-detail.component';
import { FleetGiftLogComponent } from './pages/fleet/fleet-gift-log/fleet-gift-log.component';
import { FleetRecordGiftComponent } from './pages/fleet/fleet-record-gift/fleet-record-gift.component';
import { FleetEmergencyListComponent } from './pages/fleet/fleet-emergency-list/fleet-emergency-list.component';
import { FleetChatListComponent } from './pages/fleet/fleet-chat-list/fleet-chat-list.component';
import { FleetRuleListComponent } from './pages/fleet/fleet-rule-list/fleet-rule-list.component';
import { FleetRuleCreateComponent } from './pages/fleet/fleet-rule-create/fleet-rule-create.component';
import { FleetRuleEditComponent } from './pages/fleet/fleet-rule-edit/fleet-rule-edit.component';
import { FleetLibraryHubComponent } from './pages/fleet/fleet-library/fleet-library-hub/fleet-library-hub.component';
import { FleetLibraryListComponent } from './pages/fleet/fleet-library/fleet-library-list/fleet-library-list.component';
import { FleetLibraryDetailComponent } from './pages/fleet/fleet-library/fleet-library-detail/fleet-library-detail.component';
import { FleetForumListComponent } from './pages/fleet/fleet-forums/fleet-forum-list/fleet-forum-list.component';
import { FleetForumCreateComponent } from './pages/fleet/fleet-forums/fleet-forum-create/fleet-forum-create.component';
import { FleetForumDetailComponent } from './pages/fleet/fleet-forums/fleet-forum-detail/fleet-forum-detail.component';
import { HowToUseHubComponent } from './pages/how-to-use/how-to-use-hub/how-to-use-hub.component';
import { HowToUseTopicComponent } from './pages/how-to-use/how-to-use-topic/how-to-use-topic.component';

export const routes: Routes = [
  {
    path: '',
    component: ProductLandingComponent,
    title: 'Home'
  },
  {
    path: 'sign-in',
    component: SignInComponent,
    title: 'Sign In'
  },
  {
    path: 'sign-up',
    component: SignUpComponent,
    title: 'Sign Up'
  },
  {
    path: 'forgot-password',
    component: ForgotPasswordComponent,
    title: 'Forgot Password'
  },
  {
    path: 'reset-password',
    component: ResetPasswordComponent,
    title: 'Reset Password'
  },
  {
    path: 'app/crew',
    component: CrewHomeComponent,
    pathMatch: 'full',
    canActivate: [authGuard],
    title: 'Crew'
  },
  {
    path: 'app/how-to',
    component: HowToUseHubComponent,
    pathMatch: 'full',
    canActivate: [authGuard],
    title: 'How to use this app',
    data: { parentTab: 'crew', locationHeader: 'How to use this app' }
  },
  {
    path: 'app/how-to/:topicId',
    component: HowToUseTopicComponent,
    canActivate: [authGuard],
    title: 'How to use this app',
    data: { parentTab: 'crew', locationHeader: 'How to use this app' }
  },
  {
    path: 'app/fleet',
    component: FleetHomeComponent,
    pathMatch: 'full',
    canActivate: [authGuard],
    title: 'Fleet'
  },
  {
    path: 'app/fleet/create',
    component: CreateFleetComponent,
    canActivate: [authGuard],
    data: { parentTab: 'fleet', locationHeader: 'Create Fleet' }
  },
  {
    path: 'app/fleet/join',
    component: JoinFleetComponent,
    canActivate: [authGuard],
    data: { parentTab: 'fleet', locationHeader: 'Join Fleet' }
  },
  {
    path: 'app/fleet/accept-rules',
    component: AcceptFleetRulesComponent,
    canActivate: [authGuard],
    data: { parentTab: 'fleet', locationHeader: 'Accept Rules' }
  },
  {
    path: 'app/fleet/join-requests',
    component: FleetJoinRequestsComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Join Requests' }
  },
  {
    path: 'app/fleet/edit',
    component: EditFleetComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Fleet Settings' }
  },
  {
    path: 'app/fleet/gift-log',
    component: FleetGiftLogComponent,
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Gift Log' }
  },
  {
    path: 'app/fleet/gift-log/record',
    component: FleetRecordGiftComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Record Gift' }
  },
  {
    path: 'app/fleet/emergency-requests',
    component: FleetEmergencyListComponent,
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Emergency Requests' }
  },
  {
    path: 'app/fleet/library',
    component: FleetLibraryHubComponent,
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Library of Things' }
  },
  {
    path: 'app/fleet/library/durable',
    component: FleetLibraryListComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { title: 'Durable Goods', kind: 'Durable', parentTab: 'fleet', locationHeader: 'Durable Goods' }
  },
  {
    path: 'app/fleet/library/consumable',
    component: FleetLibraryListComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { title: 'Consumable Goods', kind: 'Consumable', parentTab: 'fleet', locationHeader: 'Consumable Goods' }
  },
  {
    path: 'app/fleet/library/services',
    component: FleetLibraryListComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { title: 'Services', kind: 'Service', parentTab: 'fleet', locationHeader: 'Services' }
  },
  {
    path: 'app/fleet/library/units/:unitId',
    component: FleetLibraryDetailComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Library Item' }
  },
  {
    path: 'app/fleet/chats',
    component: FleetChatListComponent,
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Chats' }
  },
  {
    path: 'app/fleet/chats/arrange',
    component: ArrangeChatChannelsComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Arrange Channels' }
  },
  {
    path: 'app/fleet/chats/create',
    component: ChatCreateComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Create Chat' }
  },
  {
    path: 'app/fleet/chats/:id',
    component: ChatTextComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Chat' }
  },
  {
    path: 'app/fleet/forums',
    component: FleetForumListComponent,
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Fleet Space' }
  },
  {
    path: 'app/fleet/forums/create',
    component: FleetForumCreateComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Create Post' }
  },
  {
    path: 'app/fleet/forums/:id',
    component: FleetForumDetailComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Post' }
  },
  {
    path: 'app/fleet/proposals',
    component: ProposalsTypeComponent,
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Proposals' }
  },
  {
    path: 'app/fleet/proposals/list/:status',
    component: ProposalsListComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Proposals' }
  },
  {
    path: 'app/fleet/proposals/create',
    component: CreateProposalComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Create Proposal' }
  },
  {
    path: 'app/fleet/proposals/:id',
    component: ProposalDetailComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Proposal' }
  },
  {
    path: 'app/fleet/rules',
    component: FleetRuleListComponent,
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Rules' }
  },
  {
    path: 'app/fleet/rules/create',
    component: FleetRuleCreateComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Create Rule' }
  },
  {
    path: 'app/fleet/rules/:id/edit',
    component: FleetRuleEditComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Edit Rule' }
  },
  {
    path: 'app/fleet/crews',
    component: FleetCrewsComponent,
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Crews' }
  },
  {
    path: 'app/fleet/crews/invite',
    component: InviteCrewComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Add Crew' }
  },
  {
    path: 'app/fleet/crews/:id',
    component: FleetCrewDetailComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Crew' }
  },
  {
    path: 'app/fleet/crewmates/:userId',
    component: FleetCrewmateDetailComponent,
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Crewmate' }
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
    canActivate: [authGuard],
    data: { parentTab: 'friends', locationHeader: 'Requests' }
  },
  {
    path: 'app/friends/blocked',
    component: FriendBlockedComponent,
    canActivate: [authGuard],
    data: { parentTab: 'friends', locationHeader: 'Blocked' }
  },
  {
    path: 'app/friends/find',
    component: FindFriendComponent,
    canActivate: [authGuard],
    data: { parentTab: 'friends', locationHeader: 'Find Friend' }
  },
  {
    path: 'app/friends/messages/:userId',
    component: FriendDmComponent,
    canActivate: [authGuard],
    data: { parentTab: 'friends', locationHeader: 'Message' }
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
    canActivate: [authGuard],
    title: 'Profile'
  },
  {
    path: 'app/profile/user',
    component: ProfileComponent,
    canActivate: [authGuard],
    title: 'Edit Profile'
  ,
    data: { parentTab: 'profile', locationHeader: 'Edit Profile' }
  },
  {
    path: 'app/donate',
    component: DonateComponent,
    canActivate: [authGuard],
    title: 'Donate'
  ,
    data: { parentTab: 'profile', locationHeader: 'Donate' }
  },
  {
    path: 'app/profile/gift-history',
    component: GiftHistoryListComponent,
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Gift History' }
  },
  {
    path: 'app/profile/gift-history/:userId',
    component: GiftHistoryDetailComponent,
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Gift History' }
  },
  {
    path: 'app/profile/activity',
    component: ActivityCenterComponent,
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Activity' }
  },
  {
    path: 'app/profile/preferences',
    component: ProfileSettingsComponent,
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Preferences' }
  },
  {
    path: 'app/profile/preferences/notifications',
    component: NotificationSettingsComponent,
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Notifications' }
  },
  {
    path: 'app/profile/preferences/content',
    component: ContentSettingsComponent,
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Content' }
  },
  {
    path: 'app/profile/preferences/voice',
    component: VoiceSettingsComponent,
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Voice' }
  },
  {
    path: 'app/profile/preferences/theme',
    component: ThemeSettingsComponent,
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Theme' }
  },
  {
    path: 'app/profile/preferences/security',
    component: SecuritySettingsComponent,
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Security' }
  },
  {
    path: 'app/profile/preferences/security/alerts',
    component: SecurityAlertsComponent,
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Security Alerts' }
  },
  {
    path: 'app/profile/preferences/security/password',
    component: PasswordUpdateComponent,
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Password' }
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
    path: 'app/crew/create',
    component: CreateCrewComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Create Crew' }
  },
  {
    path: 'app/crew/join',
    component: JoinCrewComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Join Crew' }
  },
  {
    path: 'app/crew/join-requests',
    component: MyJoinRequestsComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Join Requests' }
  },
  {
    path: 'app/crew/invitations',
    component: MyInvitationsComponent,
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Invitations' }
  },
  {
    path: 'app/crew/edit',
    component: EditCrewComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Crew Settings' }
  },
  {
    path: 'app/crew/gift-log',
    component: GiftLogComponent,
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Gift Log' }
  },
  {
    path: 'app/crew/emergency-requests',
    component: EmergencyRequestsListComponent,
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Emergency Requests' }
  },
  {
    path: 'app/crew/emergency-requests/create',
    component: EmergencyRequestCreateComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Create Emergency Request' }
  },
  {
    path: 'app/crew/emergency-requests/:id',
    component: EmergencyRequestDetailComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Emergency Request' }
  },
  {
    path: 'app/crew/season-setup',
    component: SeasonSetupComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Season Setup' }
  },
  {
    path: 'app/crew/join-season',
    component: JoinSeasonComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Join Season' }
  },
  {
    path: 'app/crew/gift-log/record/add-non-crewmate',
    component: AddNonCrewmateComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Add Non-Member' }
  },
  {
    path: 'app/crew/gift-log/season-info',
    component: SeasonInfoComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'My giving info' }
  },
  {
    path: 'app/crew/gift-log/record',
    component: RecordGiftComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Record Gift' }
  },
  {
    path: 'app/crew/gift-log/:id',
    component: GiftLogDetailComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Gift' }
  },
  {
    path: 'app/crew/chats',
    component: ChatListComponent,
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Chats' }
  },
  {
    path: 'app/crew/chats/arrange',
    component: ArrangeChatChannelsComponent,
    canActivate: [authGuard],
    data: { scope: 'crew', parentTab: 'crew', locationHeader: 'Arrange Channels' }
  },
  {
    path: 'app/crew/chats/create',
    component: ChatCreateComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Create Chat' }
  },
  {
    path: 'app/crew/chats/:id/edit',
    component: ChatEditComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Edit Chat' }
  },
  {
    path: 'app/crew/chats/:id/voice',
    component: ChatVoiceComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Voice Chat' }
  },
  {
    path: 'app/crew/chats/:id',
    component: ChatTextComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Chat' }
  },
  {
    path: 'app/crew/proposals',
    component: ProposalsTypeComponent,
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Proposals' }
  },
  {
    path: 'app/crew/proposals/list/:status',
    component: ProposalsListComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Proposals' }
  },
  {
    path: 'app/crew/proposals/create',
    component: CreateProposalComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Create Proposal' }
  },
  {
    path: 'app/crew/proposals/:id',
    component: ProposalDetailComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Proposal' }
  },
  {
    path: 'app/crew/forums',
    component: DiscussionListComponent,
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { discussionKind: 'forums', parentTab: 'crew', locationHeader: 'Crew Space' }
  },
  {
    path: 'app/crew/forums/create',
    component: DiscussionCreateComponent,
    canActivate: [authGuard],
    data: { discussionKind: 'forums', parentTab: 'crew', locationHeader: 'Create Post' }
  },
  {
    path: 'app/crew/forums/:id',
    component: DiscussionDetailComponent,
    canActivate: [authGuard],
    data: { discussionKind: 'forums', parentTab: 'crew', locationHeader: 'Post' }
  },
  {
    path: 'app/crew/crewmates',
    component: CrewmateListComponent,
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Crewmates' }
  },
  {
    path: 'app/crew/crewmates/invite',
    component: InviteCrewmateComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Add Crewmate' }
  },
  {
    path: 'app/crew/crewmates/kicked',
    component: KickedCrewmatesListComponent,
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Kicked Crewmates' }
  },
  {
    path: 'app/crew/invitations/:id',
    component: CrewInvitationComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Invitation' }
  },
  {
    path: 'app/crew/crewmates/:id/nominate-roles',
    component: NominateRolesComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Nominate Roles' }
  },
  {
    path: 'app/crew/crewmates/:id',
    component: CrewmateDetailComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Crewmate' }
  },
  {
    path: 'app/crew/rules',
    component: RuleListComponent,
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Rules' }
  },
  {
    path: 'app/crew/rules/create',
    component: RuleCreateComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Create Rule' }
  },
  {
    path: 'app/crew/rules/:id/edit',
    component: RuleEditComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Edit Rule' }
  },
  {
    path: 'app/crew/library-of-things/unlock',
    component: LibraryUnlockComponent,
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Unlock Library' }
  },
  {
    path: 'app/crew/library-of-things',
    component: LibraryHubComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Library of Things' }
  },
  {
    path: 'app/crew/library-of-things/tasks/create',
    component: LibraryTaskFormComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Create Quest' }
  },
  {
    path: 'app/crew/library-of-things/tasks/no-deadline',
    component: LibraryTaskNoDeadlineComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'No-Deadline Quests' }
  },
  {
    path: 'app/crew/library-of-things/tasks/:id/edit',
    component: LibraryTaskFormComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Edit Quest' }
  },
  {
    path: 'app/crew/library-of-things/tasks/:id',
    component: LibraryTaskDetailComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Quest' }
  },
  {
    path: 'app/crew/library-of-things/tasks',
    component: LibraryTaskBoardComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Quest Board' }
  },
  {
    path: 'app/crew/library-of-things/durable',
    component: LibraryDurableListComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Durable Goods' }
  },
  {
    path: 'app/crew/library-of-things/offerings/:offeringId/units',
    component: LibraryOfferingUnitsComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Durable Goods' }
  },
  {
    path: 'app/crew/library-of-things/requests/mine',
    component: LibraryMyRequestsComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'My Requests' }
  },
  {
    path: 'app/crew/library-of-things/requests/mine/pending',
    component: LibraryMyRequestStatusComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Pending Requests', requestStatus: 'Open' }
  },
  {
    path: 'app/crew/library-of-things/requests/mine/fulfilled',
    component: LibraryMyRequestStatusComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Fulfilled Requests', requestStatus: 'Fulfilled' }
  },
  {
    path: 'app/crew/library-of-things/requests/denied',
    component: LibraryDeniedRequestsComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Denied Requests' }
  },
  {
    path: 'app/crew/library-of-things/requests/:id/chat',
    component: LibraryRequestChatComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Request Chat' }
  },
  {
    path: 'app/crew/library-of-things/requests/:id',
    component: LibraryRequestDetailComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Request' }
  },
  {
    path: 'app/crew/library-of-things/requests',
    component: LibraryIncomingRequestsComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Incoming Requests' }
  },
  {
    path: 'app/crew/library-of-things/units/:unitId/active-requests',
    component: LibraryUnitActiveRequestsComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Active Requests' }
  },
  {
    path: 'app/crew/library-of-things/consumable',
    component: LibraryStockListComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { title: 'Consumable Goods', stockKind: 'Consumable', parentTab: 'crew', locationHeader: 'Consumable Goods' }
  },
  {
    path: 'app/crew/library-of-things/services',
    component: LibraryStockListComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { title: 'Services', stockKind: 'Service', parentTab: 'crew', locationHeader: 'Services' }
  },
  {
    path: 'app/crew/library-of-things/digital',
    component: LibraryStockListComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { title: 'Digital Goods', stockKind: 'Digital', parentTab: 'crew', locationHeader: 'Digital Goods' }
  },
  {
    path: 'app/crew/library-of-things/mine',
    component: LibraryMyOfferingsComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'My Offerings' }
  },
  {
    path: 'app/crew/library-of-things/offerings/create',
    component: CreateLibraryOfferingComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Create Offering' }
  },
  {
    path: 'app/crew/library-of-things/offerings/:id/edit',
    component: EditLibraryOfferingComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Edit Offering' }
  },
  {
    path: 'app/crew/library-of-things/units/:id',
    component: LibraryUnitDetailComponent,
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Library Item' }
  },
  {
    path: '**',
    redirectTo: ''
  }
];
