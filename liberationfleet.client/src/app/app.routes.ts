import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { guestGuard } from './guards/guest.guard';
import { libraryAccessGuard } from './guards/library-access.guard';
import { fleetRulesAcceptedGuard } from './guards/fleet-rules-accepted.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/product-landing/product-landing.component').then(m => m.ProductLandingComponent),
    canActivate: [guestGuard],
    title: 'Home'
  },
  {
    path: 'sign-in',
    loadComponent: () => import('./pages/sign-in/sign-in.component').then(m => m.SignInComponent),
    canActivate: [guestGuard],
    title: 'Sign In'
  },
  {
    path: 'sign-up',
    loadComponent: () => import('./pages/sign-up/sign-up.component').then(m => m.SignUpComponent),
    title: 'Sign Up'
  },
  {
    path: 'forgot-password',
    loadComponent: () => import('./pages/forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent),
    title: 'Forgot Password'
  },
  {
    path: 'reset-password',
    loadComponent: () => import('./pages/reset-password/reset-password.component').then(m => m.ResetPasswordComponent),
    title: 'Reset Password'
  },
  {
    path: 'how-to',
    loadComponent: () => import('./pages/how-to-use/how-to-use-hub/how-to-use-hub.component').then(m => m.HowToUseHubComponent),
    pathMatch: 'full',
    title: 'How to use this app'
  },
  {
    path: 'how-to/:topicId',
    loadComponent: () => import('./pages/how-to-use/how-to-use-topic/how-to-use-topic.component').then(m => m.HowToUseTopicComponent),
    title: 'How to use this app'
  },
  {
    path: 'privacy',
    loadComponent: () => import('./pages/legal-document/legal-document.component').then(m => m.LegalDocumentComponent),
    title: 'Privacy Policy',
    data: {
      documentTitle: 'Privacy Policy',
      assetPath: '/assets/privacy-policy.txt'
    }
  },
  {
    path: 'delete-account',
    loadComponent: () => import('./pages/legal-document/legal-document.component').then(m => m.LegalDocumentComponent),
    title: 'Delete your account',
    data: {
      documentTitle: 'Delete your account',
      assetPath: '/assets/account-deletion.txt'
    }
  },
  {
    path: 'terms',
    loadComponent: () => import('./pages/legal-document/legal-document.component').then(m => m.LegalDocumentComponent),
    title: 'Terms of Use',
    data: {
      documentTitle: 'Terms of Use',
      assetPath: '/assets/terms-of-use.txt'
    }
  },
  {
    path: 'community-standards',
    loadComponent: () => import('./pages/legal-document/legal-document.component').then(m => m.LegalDocumentComponent),
    title: 'Community Standards',
    data: {
      documentTitle: 'Community Standards',
      assetPath: '/assets/community-standards.txt'
    }
  },
  {
    path: 'app/crew',
    loadComponent: () => import('./pages/crew-home/crew-home.component').then(m => m.CrewHomeComponent),
    pathMatch: 'full',
    canActivate: [authGuard],
    title: 'Crew'
  },
  {
    path: 'app/how-to',
    loadComponent: () => import('./pages/how-to-use/how-to-use-hub/how-to-use-hub.component').then(m => m.HowToUseHubComponent),
    pathMatch: 'full',
    canActivate: [authGuard],
    title: 'How to use this app',
    data: { parentTab: 'profile', locationHeader: 'How to use this app' }
  },
  {
    path: 'app/how-to/:topicId',
    loadComponent: () => import('./pages/how-to-use/how-to-use-topic/how-to-use-topic.component').then(m => m.HowToUseTopicComponent),
    canActivate: [authGuard],
    title: 'How to use this app',
    data: { parentTab: 'profile', locationHeader: 'How to use this app' }
  },
  {
    path: 'app/fleet',
    loadComponent: () => import('./pages/fleet/fleet-home/fleet-home.component').then(m => m.FleetHomeComponent),
    pathMatch: 'full',
    canActivate: [authGuard],
    title: 'Fleet'
  },
  {
    path: 'app/fleet/create',
    loadComponent: () => import('./pages/fleet/create-fleet/create-fleet.component').then(m => m.CreateFleetComponent),
    canActivate: [authGuard],
    data: { parentTab: 'fleet', locationHeader: 'Create Fleet' }
  },
  {
    path: 'app/fleet/join',
    loadComponent: () => import('./pages/fleet/join-fleet/join-fleet.component').then(m => m.JoinFleetComponent),
    canActivate: [authGuard],
    data: { parentTab: 'fleet', locationHeader: 'Join Fleet' }
  },
  {
    path: 'app/fleet/accept-rules',
    loadComponent: () => import('./pages/fleet/accept-fleet-rules/accept-fleet-rules.component').then(m => m.AcceptFleetRulesComponent),
    canActivate: [authGuard],
    data: { parentTab: 'fleet', locationHeader: 'Accept Rules' }
  },
  {
    path: 'app/fleet/join-requests',
    loadComponent: () => import('./pages/fleet/fleet-join-requests/fleet-join-requests.component').then(m => m.FleetJoinRequestsComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Join Requests' }
  },
  {
    path: 'app/fleet/edit',
    loadComponent: () => import('./pages/fleet/edit-fleet/edit-fleet.component').then(m => m.EditFleetComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Fleet Settings' }
  },
  {
    path: 'app/fleet/gift-log',
    loadComponent: () => import('./pages/gift-log/gift-log.component').then(m => m.GiftLogComponent),
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Gift Log', giftLogScope: 'fleet' }
  },
  {
    path: 'app/fleet/gift-log/record',
    loadComponent: () => import('./pages/fleet/fleet-record-gift/fleet-record-gift.component').then(m => m.FleetRecordGiftComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Record Gift' }
  },
  {
    path: 'app/fleet/gift-log/:id',
    loadComponent: () => import('./pages/gift-log/gift-log-detail/gift-log-detail.component').then(m => m.GiftLogDetailComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Gift', giftLogScope: 'fleet' }
  },
  {
    path: 'app/fleet/emergency-requests',
    loadComponent: () => import('./pages/fleet/fleet-emergency-list/fleet-emergency-list.component').then(m => m.FleetEmergencyListComponent),
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Emergency Requests' }
  },
  {
    path: 'app/fleet/library',
    loadComponent: () => import('./pages/fleet/fleet-library/fleet-library-hub/fleet-library-hub.component').then(m => m.FleetLibraryHubComponent),
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Library of Things' }
  },
  {
    path: 'app/fleet/library/favorites',
    loadComponent: () => import('./pages/library/library-favorites/library-favorites.component').then(m => m.LibraryFavoritesComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Favorites', scope: 'fleet' }
  },
  {
    path: 'app/fleet/library/durable',
    loadComponent: () => import('./pages/fleet/fleet-library/fleet-library-list/fleet-library-list.component').then(m => m.FleetLibraryListComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { title: 'Durable Goods', kind: 'Durable', parentTab: 'fleet', locationHeader: 'Durable Goods' }
  },
  {
    path: 'app/fleet/library/consumable',
    loadComponent: () => import('./pages/fleet/fleet-library/fleet-library-list/fleet-library-list.component').then(m => m.FleetLibraryListComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { title: 'Consumable Goods', kind: 'Consumable', parentTab: 'fleet', locationHeader: 'Consumable Goods' }
  },
  {
    path: 'app/fleet/library/services',
    loadComponent: () => import('./pages/fleet/fleet-library/fleet-library-list/fleet-library-list.component').then(m => m.FleetLibraryListComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { title: 'Services', kind: 'Service', parentTab: 'fleet', locationHeader: 'Services' }
  },
  {
    path: 'app/fleet/library/units/:unitId',
    loadComponent: () => import('./pages/fleet/fleet-library/fleet-library-detail/fleet-library-detail.component').then(m => m.FleetLibraryDetailComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Library Item' }
  },
  {
    path: 'app/fleet/chats',
    loadComponent: () => import('./pages/fleet/fleet-chat-list/fleet-chat-list.component').then(m => m.FleetChatListComponent),
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Chats' }
  },
  {
    path: 'app/fleet/chats/arrange',
    loadComponent: () => import('./pages/chats/arrange-chat-channels/arrange-chat-channels.component').then(m => m.ArrangeChatChannelsComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Arrange Channels' }
  },
  {
    path: 'app/fleet/chats/create',
    loadComponent: () => import('./pages/chats/chat-create/chat-create.component').then(m => m.ChatCreateComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Create Chat' }
  },
  {
    path: 'app/fleet/chats/:id/edit',
    loadComponent: () => import('./pages/chats/chat-edit/chat-edit.component').then(m => m.ChatEditComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Edit Chat' }
  },
  {
    path: 'app/fleet/chats/:id',
    loadComponent: () => import('./pages/chats/chat-text/chat-text.component').then(m => m.ChatTextComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Chat' }
  },
  {
    path: 'app/fleet/forums',
    loadComponent: () => import('./pages/fleet/fleet-forums/fleet-forum-list/fleet-forum-list.component').then(m => m.FleetForumListComponent),
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Fleet Space' }
  },
  {
    path: 'app/fleet/forums/create',
    loadComponent: () => import('./pages/fleet/fleet-forums/fleet-forum-create/fleet-forum-create.component').then(m => m.FleetForumCreateComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Create Post' }
  },
  {
    path: 'app/fleet/forums/:id',
    loadComponent: () => import('./pages/fleet/fleet-forums/fleet-forum-detail/fleet-forum-detail.component').then(m => m.FleetForumDetailComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Post' }
  },
  {
    path: 'app/fleet/proposals',
    loadComponent: () => import('./pages/proposals/proposals-type/proposals-type.component').then(m => m.ProposalsTypeComponent),
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Proposals' }
  },
  {
    path: 'app/fleet/proposals/list/:status',
    loadComponent: () => import('./pages/proposals/proposals-list/proposals-list.component').then(m => m.ProposalsListComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Proposals' }
  },
  {
    path: 'app/fleet/proposals/create',
    loadComponent: () => import('./pages/proposals/create-proposal/create-proposal.component').then(m => m.CreateProposalComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Create Proposal' }
  },
  {
    path: 'app/fleet/proposals/:id',
    loadComponent: () => import('./pages/proposals/proposal-detail/proposal-detail.component').then(m => m.ProposalDetailComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { scope: 'fleet', parentTab: 'fleet', locationHeader: 'Proposal' }
  },
  {
    path: 'app/fleet/rules',
    loadComponent: () => import('./pages/fleet/fleet-rule-list/fleet-rule-list.component').then(m => m.FleetRuleListComponent),
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Rules' }
  },
  {
    path: 'app/fleet/rules/create',
    loadComponent: () => import('./pages/fleet/fleet-rule-create/fleet-rule-create.component').then(m => m.FleetRuleCreateComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Create Rule' }
  },
  {
    path: 'app/fleet/rules/:id/edit',
    loadComponent: () => import('./pages/fleet/fleet-rule-edit/fleet-rule-edit.component').then(m => m.FleetRuleEditComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Edit Rule' }
  },
  {
    path: 'app/fleet/crews',
    loadComponent: () => import('./pages/fleet/fleet-crews/fleet-crews.component').then(m => m.FleetCrewsComponent),
    pathMatch: 'full',
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Crews' }
  },
  {
    path: 'app/fleet/crews/invite',
    loadComponent: () => import('./pages/fleet/invite-crew/invite-crew.component').then(m => m.InviteCrewComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Add Crew' }
  },
  {
    path: 'app/fleet/crews/:id',
    loadComponent: () => import('./pages/fleet/fleet-crew-detail/fleet-crew-detail.component').then(m => m.FleetCrewDetailComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Crew' }
  },
  {
    path: 'app/fleet/crewmates/:userId',
    loadComponent: () => import('./pages/fleet/fleet-crewmate-detail/fleet-crewmate-detail.component').then(m => m.FleetCrewmateDetailComponent),
    canActivate: [authGuard, fleetRulesAcceptedGuard],
    data: { parentTab: 'fleet', locationHeader: 'Crewmate' }
  },
  {
    path: 'app/friends',
    loadComponent: () => import('./pages/friends/friends.component').then(m => m.FriendsComponent),
    pathMatch: 'full',
    canActivate: [authGuard]
  },
  {
    path: 'app/friends/requests',
    loadComponent: () => import('./pages/friends/friend-requests/friend-requests.component').then(m => m.FriendRequestsComponent),
    canActivate: [authGuard],
    data: { parentTab: 'friends', locationHeader: 'Requests' }
  },
  {
    path: 'app/friends/blocked',
    loadComponent: () => import('./pages/friends/friend-blocked/friend-blocked.component').then(m => m.FriendBlockedComponent),
    canActivate: [authGuard],
    data: { parentTab: 'friends', locationHeader: 'Blocked' }
  },
  {
    path: 'app/friends/find',
    loadComponent: () => import('./pages/friends/find-friend/find-friend.component').then(m => m.FindFriendComponent),
    canActivate: [authGuard],
    data: { parentTab: 'friends', locationHeader: 'Find Friend' }
  },
  {
    path: 'app/friends/messages/:userId',
    loadComponent: () => import('./pages/friends/friend-dm/friend-dm.component').then(m => m.FriendDmComponent),
    canActivate: [authGuard],
    data: { parentTab: 'friends', locationHeader: 'Message' }
  },
  {
    path: 'app/notifications',
    loadComponent: () => import('./pages/notifications/notifications.component').then(m => m.NotificationsComponent),
    canActivate: [authGuard]
  },
  {
    path: 'app/profile',
    loadComponent: () => import('./pages/user-home/user-home.component').then(m => m.UserHomeComponent),
    pathMatch: 'full',
    canActivate: [authGuard],
    title: 'Profile'
  },
  {
    path: 'app/profile/user',
    loadComponent: () => import('./pages/profile/profile.component').then(m => m.ProfileComponent),
    canActivate: [authGuard],
    title: 'Edit Profile'
  ,
    data: { parentTab: 'profile', locationHeader: 'Edit Profile' }
  },
  {
    path: 'app/donate',
    loadComponent: () => import('./pages/donate/donate.component').then(m => m.DonateComponent),
    // Guests may donate with a receipt email; signed-in users use account email.
    title: 'Donate'
  ,
    data: { parentTab: 'profile', locationHeader: 'Donate' }
  },
  {
    path: 'app/profile/gift-history',
    loadComponent: () => import('./pages/profile/gift-history-list/gift-history-list.component').then(m => m.GiftHistoryListComponent),
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Gift History' }
  },
  {
    path: 'app/profile/gift-history/:userId',
    loadComponent: () => import('./pages/profile/gift-history-detail/gift-history-detail.component').then(m => m.GiftHistoryDetailComponent),
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Gift History' }
  },
  {
    path: 'app/profile/activity',
    loadComponent: () => import('./pages/activity-center/activity-center.component').then(m => m.ActivityCenterComponent),
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Activity' }
  },
  {
    path: 'app/profile/preferences',
    loadComponent: () => import('./pages/profile-settings/profile-settings.component').then(m => m.ProfileSettingsComponent),
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Preferences' }
  },
  {
    path: 'app/profile/preferences/notifications',
    loadComponent: () => import('./pages/notification-settings/notification-settings.component').then(m => m.NotificationSettingsComponent),
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Notifications' }
  },
  {
    path: 'app/profile/preferences/content',
    loadComponent: () => import('./pages/content-settings/content-settings.component').then(m => m.ContentSettingsComponent),
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Content' }
  },
  {
    path: 'app/profile/preferences/voice',
    loadComponent: () => import('./pages/voice-settings/voice-settings.component').then(m => m.VoiceSettingsComponent),
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Voice' }
  },
  {
    path: 'app/profile/preferences/theme',
    loadComponent: () => import('./pages/theme-settings/theme-settings.component').then(m => m.ThemeSettingsComponent),
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Theme' }
  },
  {
    path: 'app/profile/preferences/security',
    loadComponent: () => import('./pages/security-settings/security-settings.component').then(m => m.SecuritySettingsComponent),
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Security' }
  },
  {
    path: 'app/profile/preferences/security/alerts',
    loadComponent: () => import('./pages/security-alerts/security-alerts.component').then(m => m.SecurityAlertsComponent),
    canActivate: [authGuard],
    data: { parentTab: 'profile', locationHeader: 'Security Alerts' }
  },
  {
    path: 'app/profile/preferences/security/password',
    loadComponent: () => import('./pages/password-update/password-update.component').then(m => m.PasswordUpdateComponent),
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
    path: 'app/crew/find',
    loadComponent: () => import('./pages/crew-find/crew-find.component').then(m => m.CrewFindComponent),
    canActivate: [authGuard],
    title: 'Join or create a crew',
    data: { parentTab: 'crew', locationHeader: 'Join/Create a crew' }
  },
  {
    path: 'app/crew/create',
    loadComponent: () => import('./pages/create-crew/create-crew.component').then(m => m.CreateCrewComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Create Crew' }
  },
  {
    path: 'app/crew/join',
    loadComponent: () => import('./pages/join-crew/join-crew.component').then(m => m.JoinCrewComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Join Crew' }
  },
  {
    path: 'app/crew/join-requests',
    loadComponent: () => import('./pages/my-join-requests/my-join-requests.component').then(m => m.MyJoinRequestsComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Join Requests' }
  },
  {
    path: 'app/crew/invitations',
    loadComponent: () => import('./pages/my-invitations/my-invitations.component').then(m => m.MyInvitationsComponent),
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Invitations' }
  },
  {
    path: 'app/crew/edit',
    loadComponent: () => import('./pages/edit-crew/edit-crew.component').then(m => m.EditCrewComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Crew Settings' }
  },
  {
    path: 'app/crew/gift-log',
    loadComponent: () => import('./pages/gift-log/gift-log.component').then(m => m.GiftLogComponent),
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Gift Log' }
  },
  {
    path: 'app/crew/emergency-requests',
    loadComponent: () => import('./pages/emergency-requests/emergency-requests-list/emergency-requests-list.component').then(m => m.EmergencyRequestsListComponent),
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Emergency Requests' }
  },
  {
    path: 'app/crew/emergency-requests/create',
    loadComponent: () => import('./pages/emergency-requests/emergency-request-create/emergency-request-create.component').then(m => m.EmergencyRequestCreateComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Create Emergency Request' }
  },
  {
    path: 'app/crew/emergency-requests/:id',
    loadComponent: () => import('./pages/emergency-requests/emergency-request-detail/emergency-request-detail.component').then(m => m.EmergencyRequestDetailComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Emergency Request' }
  },
  {
    path: 'app/crew/season-setup',
    loadComponent: () => import('./pages/season-setup/season-setup.component').then(m => m.SeasonSetupComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Season Setup' }
  },
  {
    path: 'app/crew/join-season',
    loadComponent: () => import('./pages/join-season/join-season.component').then(m => m.JoinSeasonComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Join Season' }
  },
  {
    path: 'app/crew/gift-log/record/add-non-crewmate',
    loadComponent: () => import('./pages/record-gift/add-non-crewmate/add-non-crewmate.component').then(m => m.AddNonCrewmateComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Add Non-Member' }
  },
  {
    path: 'app/crew/gift-log/season-info',
    loadComponent: () => import('./pages/gift-log/season-info/season-info.component').then(m => m.SeasonInfoComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'My giving info' }
  },
  {
    path: 'app/crew/gift-log/record',
    loadComponent: () => import('./pages/record-gift/record-gift.component').then(m => m.RecordGiftComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Record Gift' }
  },
  {
    path: 'app/crew/gift-log/record/reception-order',
    loadComponent: () => import('./pages/record-gift/edit-reception-order/edit-reception-order.component').then(m => m.EditReceptionOrderComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Edit Reception Order' }
  },
  {
    path: 'app/crew/gift-log/:id',
    loadComponent: () => import('./pages/gift-log/gift-log-detail/gift-log-detail.component').then(m => m.GiftLogDetailComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Gift' }
  },
  {
    path: 'app/crew/chats',
    loadComponent: () => import('./pages/chats/chat-list/chat-list.component').then(m => m.ChatListComponent),
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Chats' }
  },
  {
    path: 'app/crew/chats/arrange',
    loadComponent: () => import('./pages/chats/arrange-chat-channels/arrange-chat-channels.component').then(m => m.ArrangeChatChannelsComponent),
    canActivate: [authGuard],
    data: { scope: 'crew', parentTab: 'crew', locationHeader: 'Arrange Channels' }
  },
  {
    path: 'app/crew/chats/create',
    loadComponent: () => import('./pages/chats/chat-create/chat-create.component').then(m => m.ChatCreateComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Create Chat' }
  },
  {
    path: 'app/crew/chats/:id/edit',
    loadComponent: () => import('./pages/chats/chat-edit/chat-edit.component').then(m => m.ChatEditComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Edit Chat' }
  },
  {
    path: 'app/crew/chats/:id/voice',
    loadComponent: () => import('./pages/chats/chat-voice/chat-voice.component').then(m => m.ChatVoiceComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Voice Chat' }
  },
  {
    path: 'app/crew/chats/:id',
    loadComponent: () => import('./pages/chats/chat-text/chat-text.component').then(m => m.ChatTextComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Chat' }
  },
  {
    path: 'app/crew/proposals',
    loadComponent: () => import('./pages/proposals/proposals-type/proposals-type.component').then(m => m.ProposalsTypeComponent),
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Proposals' }
  },
  {
    path: 'app/crew/proposals/list/:status',
    loadComponent: () => import('./pages/proposals/proposals-list/proposals-list.component').then(m => m.ProposalsListComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Proposals' }
  },
  {
    path: 'app/crew/proposals/create',
    loadComponent: () => import('./pages/proposals/create-proposal/create-proposal.component').then(m => m.CreateProposalComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Create Proposal' }
  },
  {
    path: 'app/crew/proposals/:id',
    loadComponent: () => import('./pages/proposals/proposal-detail/proposal-detail.component').then(m => m.ProposalDetailComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Proposal' }
  },
  {
    path: 'app/crew/forums',
    loadComponent: () => import('./pages/crew-discussion/discussion-list/discussion-list.component').then(m => m.DiscussionListComponent),
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { discussionKind: 'forums', parentTab: 'crew', locationHeader: 'Crew Space' }
  },
  {
    path: 'app/crew/forums/create',
    loadComponent: () => import('./pages/crew-discussion/discussion-create/discussion-create.component').then(m => m.DiscussionCreateComponent),
    canActivate: [authGuard],
    data: { discussionKind: 'forums', parentTab: 'crew', locationHeader: 'Create Post' }
  },
  {
    path: 'app/crew/forums/:id',
    loadComponent: () => import('./pages/crew-discussion/discussion-detail/discussion-detail.component').then(m => m.DiscussionDetailComponent),
    canActivate: [authGuard],
    data: { discussionKind: 'forums', parentTab: 'crew', locationHeader: 'Post' }
  },
  {
    path: 'app/crew/crewmates',
    loadComponent: () => import('./pages/crewmates/crewmate-list/crewmate-list.component').then(m => m.CrewmateListComponent),
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Crewmates' }
  },
  {
    path: 'app/crew/crewmates/invite',
    loadComponent: () => import('./pages/crewmates/invite-crewmate/invite-crewmate.component').then(m => m.InviteCrewmateComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Add Crewmate' }
  },
  {
    path: 'app/crew/crewmates/placeholders/create',
    loadComponent: () => import('./pages/crewmates/create-placeholder/create-placeholder.component').then(m => m.CreatePlaceholderComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Add Placeholder' }
  },
  {
    path: 'app/crew/crewmates/kicked',
    loadComponent: () => import('./pages/crewmates/kicked-crewmates-list/kicked-crewmates-list.component').then(m => m.KickedCrewmatesListComponent),
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Kicked Crewmates' }
  },
  {
    path: 'app/crew/invitations/:id',
    loadComponent: () => import('./pages/crewmates/crew-invitation/crew-invitation.component').then(m => m.CrewInvitationComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Invitation' }
  },
  {
    path: 'app/crew/crewmates/:id/nominate-roles',
    loadComponent: () => import('./pages/crewmates/nominate-roles/nominate-roles.component').then(m => m.NominateRolesComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Nominate Roles' }
  },
  {
    path: 'app/crew/crewmates/:id',
    loadComponent: () => import('./pages/crewmates/crewmate-detail/crewmate-detail.component').then(m => m.CrewmateDetailComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Crewmate' }
  },
  {
    path: 'app/crew/rules',
    loadComponent: () => import('./pages/rules/rule-list/rule-list.component').then(m => m.RuleListComponent),
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Rules' }
  },
  {
    path: 'app/crew/rules/create',
    loadComponent: () => import('./pages/rules/rule-create/rule-create.component').then(m => m.RuleCreateComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Create Rule' }
  },
  {
    path: 'app/crew/rules/:id/edit',
    loadComponent: () => import('./pages/rules/rule-edit/rule-edit.component').then(m => m.RuleEditComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Edit Rule' }
  },
  {
    path: 'app/crew/library-of-things/unlock',
    loadComponent: () => import('./pages/library/library-unlock/library-unlock.component').then(m => m.LibraryUnlockComponent),
    canActivate: [authGuard],
    data: { parentTab: 'crew', locationHeader: 'Unlock Library' }
  },
  {
    path: 'app/crew/library-of-things',
    loadComponent: () => import('./pages/library/library-hub/library-hub.component').then(m => m.LibraryHubComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Library of Things' }
  },
  {
    path: 'app/crew/library-of-things/tasks/create',
    loadComponent: () => import('./pages/library/library-tasks/library-task-form/library-task-form.component').then(m => m.LibraryTaskFormComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Create Quest' }
  },
  {
    path: 'app/crew/library-of-things/tasks/no-deadline',
    loadComponent: () => import('./pages/library/library-tasks/library-task-no-deadline/library-task-no-deadline.component').then(m => m.LibraryTaskNoDeadlineComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Quest Board' }
  },
  {
    path: 'app/crew/library-of-things/tasks/:id/edit',
    loadComponent: () => import('./pages/library/library-tasks/library-task-form/library-task-form.component').then(m => m.LibraryTaskFormComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Edit Quest' }
  },
  {
    path: 'app/crew/library-of-things/tasks/:id',
    loadComponent: () => import('./pages/library/library-tasks/library-task-detail/library-task-detail.component').then(m => m.LibraryTaskDetailComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Quest' }
  },
  {
    path: 'app/crew/library-of-things/tasks',
    loadComponent: () => import('./pages/library/library-tasks/library-task-board/library-task-board.component').then(m => m.LibraryTaskBoardComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Quest Board' }
  },
  {
    path: 'app/crew/library-of-things/favorites',
    loadComponent: () => import('./pages/library/library-favorites/library-favorites.component').then(m => m.LibraryFavoritesComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Favorites', scope: 'crew' }
  },
  {
    path: 'app/crew/library-of-things/durable',
    loadComponent: () => import('./pages/library/library-durable-list/library-durable-list.component').then(m => m.LibraryDurableListComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Durable Goods' }
  },
  {
    path: 'app/crew/library-of-things/offerings/:offeringId/units',
    loadComponent: () => import('./pages/library/library-offering-units/library-offering-units.component').then(m => m.LibraryOfferingUnitsComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Durable Goods' }
  },
  {
    path: 'app/crew/library-of-things/requests/mine',
    loadComponent: () => import('./pages/library/library-my-requests/library-my-requests.component').then(m => m.LibraryMyRequestsComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'My Requests' }
  },
  {
    path: 'app/crew/library-of-things/requests/mine/pending',
    loadComponent: () => import('./pages/library/library-my-request-status/library-my-request-status.component').then(m => m.LibraryMyRequestStatusComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Pending Requests', requestStatus: 'Open' }
  },
  {
    path: 'app/crew/library-of-things/requests/mine/fulfilled',
    loadComponent: () => import('./pages/library/library-my-request-status/library-my-request-status.component').then(m => m.LibraryMyRequestStatusComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Fulfilled Requests', requestStatus: 'Fulfilled' }
  },
  {
    path: 'app/crew/library-of-things/requests/denied',
    loadComponent: () => import('./pages/library/library-denied-requests/library-denied-requests.component').then(m => m.LibraryDeniedRequestsComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Denied Requests' }
  },
  {
    path: 'app/crew/library-of-things/requests/:id/chat',
    loadComponent: () => import('./pages/library/library-request-chat/library-request-chat.component').then(m => m.LibraryRequestChatComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Request Chat' }
  },
  {
    path: 'app/crew/library-of-things/requests/:id',
    loadComponent: () => import('./pages/library/library-request-detail/library-request-detail.component').then(m => m.LibraryRequestDetailComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Request' }
  },
  {
    path: 'app/crew/library-of-things/requests',
    loadComponent: () => import('./pages/library/library-incoming-requests/library-incoming-requests.component').then(m => m.LibraryIncomingRequestsComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Incoming Requests' }
  },
  {
    path: 'app/crew/library-of-things/units/:unitId/active-requests',
    loadComponent: () => import('./pages/library/library-unit-active-requests/library-unit-active-requests.component').then(m => m.LibraryUnitActiveRequestsComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Active Requests' }
  },
  {
    path: 'app/crew/library-of-things/consumable',
    loadComponent: () => import('./pages/library/library-stock-list/library-stock-list.component').then(m => m.LibraryStockListComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { title: 'Consumable Goods', stockKind: 'Consumable', parentTab: 'crew', locationHeader: 'Consumable Goods' }
  },
  {
    path: 'app/crew/library-of-things/services',
    loadComponent: () => import('./pages/library/library-stock-list/library-stock-list.component').then(m => m.LibraryStockListComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { title: 'Services', stockKind: 'Service', parentTab: 'crew', locationHeader: 'Services' }
  },
  {
    path: 'app/crew/library-of-things/digital',
    loadComponent: () => import('./pages/library/library-stock-list/library-stock-list.component').then(m => m.LibraryStockListComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { title: 'Digital Goods', stockKind: 'Digital', parentTab: 'crew', locationHeader: 'Digital Goods' }
  },
  {
    path: 'app/crew/library-of-things/mine',
    loadComponent: () => import('./pages/library/library-my-offerings/library-my-offerings.component').then(m => m.LibraryMyOfferingsComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'My Offerings' }
  },
  {
    path: 'app/crew/library-of-things/offerings/create',
    loadComponent: () => import('./pages/library/create-library-offering/create-library-offering.component').then(m => m.CreateLibraryOfferingComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Create Offering' }
  },
  {
    path: 'app/crew/library-of-things/offerings/:id/edit',
    loadComponent: () => import('./pages/library/edit-library-offering/edit-library-offering.component').then(m => m.EditLibraryOfferingComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Edit Offering' }
  },
  {
    path: 'app/crew/library-of-things/units/:id',
    loadComponent: () => import('./pages/library/library-unit-detail/library-unit-detail.component').then(m => m.LibraryUnitDetailComponent),
    canActivate: [authGuard, libraryAccessGuard],
    data: { parentTab: 'crew', locationHeader: 'Library Item' }
  },
  {
    path: '**',
    redirectTo: ''
  }
];
