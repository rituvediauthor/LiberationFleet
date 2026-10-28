import { Provider } from '@angular/core';
import { BehaviorSubject, of, EMPTY } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { UserService } from '../services/user.service';
import { CrewService } from '../services/crew.service';
import { GiftService } from '../services/gift.service';
import { ProfileService } from '../services/profile.service';
import { ToastService } from '../components/toast/toast.component';
import { FallibleService } from '../services/fallible.service';
import { ClientConfigService } from '../services/client-config.service';
import { NotificationService } from '../services/notification.service';
import { NotificationHubService } from '../services/notification-hub.service';
import { FleetService } from '../services/fleet.service';
import { CryptoSessionService } from '../services/crypto/crypto-session.service';
import { SecurityService } from '../services/security.service';
import { ProfileLocationService } from '../services/profile-location.service';
import { EncryptedImageCacheService } from '../services/encrypted-image-cache.service';
import { ProposalCryptoService } from '../services/crypto/proposal-crypto.service';
import { APP_ENVIRONMENT } from '../config/app-environment';
import { emptyAreaCounts } from '../utils/notification-area.util';

export function createAuthServiceMock(): jasmine.SpyObj<AuthService> {
  return jasmine.createSpyObj<AuthService>('AuthService', [
    'login',
    'logout',
    'establishSession',
    'setupNewAccountEncryption',
    'unlockWithRecoveryPhrase',
    'setRememberRecoveryPhrase',
    'getToken',
    'setToken',
    'removeToken',
    'isAuthenticated',
    'updateCurrentUser',
    'isRememberLoginEnabled',
    'setRememberLoginEnabled'
  ], {
    currentUser$: of(null)
  });
}

export function createUserServiceMock(): jasmine.SpyObj<UserService> {
  return jasmine.createSpyObj<UserService>('UserService', [
    'create',
    'requestPasswordReset',
    'validateResetToken',
    'resetPassword'
  ]);
}

export function createToastServiceMock(): jasmine.SpyObj<ToastService> {
  return jasmine.createSpyObj<ToastService>('ToastService', [
    'show',
    'remove',
    'success',
    'error',
    'info',
    'warning'
  ]);
}

import {
  AUTH_TOKEN_STORAGE_KEY,
  CREW_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY,
  CREW_MEMBERSHIP_CACHE_STORAGE_KEY,
  FLEET_STATUS_CACHE_STORAGE_KEY,
  REMEMBER_LOGIN_STORAGE_KEY,
  SESSION_RECOVERY_PHRASE_STORAGE_KEY
} from '../services/storage/storage-keys';

export function clearAuthStorage(): void {
  localStorage.removeItem(AUTH_TOKEN_STORAGE_KEY);
  localStorage.removeItem(REMEMBER_LOGIN_STORAGE_KEY);
  localStorage.removeItem(CREW_DASHBOARD_PREVIEW_CACHE_STORAGE_KEY);
  localStorage.removeItem(CREW_MEMBERSHIP_CACHE_STORAGE_KEY);
  localStorage.removeItem(FLEET_STATUS_CACHE_STORAGE_KEY);
  sessionStorage.removeItem(AUTH_TOKEN_STORAGE_KEY);
  sessionStorage.removeItem(SESSION_RECOVERY_PHRASE_STORAGE_KEY);
}

export function createCrewServiceMock(): jasmine.SpyObj<CrewService> {
  const mock = jasmine.createSpyObj<CrewService>('CrewService', [
    'getMembership',
    'create',
    'search',
    'getPublicRules',
    'getPublicRulesByJoinCode',
    'submitJoinRequest',
    'getMyJoinRequests',
    'getMyInvitations',
    'getInvitation',
    'getPaymentPlatforms',
    'clearMembershipCache',
    'clearSessionCache',
    'setCachedMembership',
    'peekPersistedMembership'
  ], {
    membershipChanged$: EMPTY
  });
  mock.peekPersistedMembership.and.returnValue(null);
  mock.getMembership.and.returnValue(of({ hasCrew: false }));
  mock.getPaymentPlatforms.and.returnValue(of([]));
  return mock;
}

export function createGiftServiceMock(): jasmine.SpyObj<GiftService> {
  const mock = jasmine.createSpyObj<GiftService>('GiftService', [
    'getNextAidInfo',
    'getSeasonStatus',
    'saveSeasonSetup',
    'getReceptionOrder',
    'getCrewMembers',
    'getPaymentPlatforms',
    'getPendingMiddlemanGifts',
    'getLogs',
    'isUserRelated',
    'recordGift',
    'recordGifts',
    'navigateToGiftLogEntry',
    'navigateToNextAidAction'
  ]);
  mock.getCrewMembers.and.returnValue(of([]));
  mock.getReceptionOrder.and.returnValue(of([]));
  mock.getSeasonStatus.and.returnValue(of({
    seasonStarted: false,
    userInSeason: false,
    userSeasonReady: false,
    readyCount: 0,
    canStartSeason: false,
    hasPendingStartSeasonProposal: false,
    pendingStartSeasonProposalId: null
  }));
  mock.saveSeasonSetup.and.returnValue(of({ success: true, message: 'Season setup saved.' }));
  mock.getPaymentPlatforms.and.returnValue(of([
    { id: 1, name: 'PayPal' },
    { id: 2, name: 'Cash App' },
    { id: 3, name: 'Venmo' },
    { id: 4, name: 'Zelle' },
    { id: 5, name: 'Other' }
  ]));
  mock.getPendingMiddlemanGifts.and.returnValue(of([]));
  mock.getLogs.and.returnValue(of({ items: [], hasMore: false }));
  mock.recordGift.and.returnValue(of({ success: true, message: 'Gift recorded' }));
  mock.recordGifts.and.returnValue(of({ success: true, message: 'Gifts recorded' }));
  return mock;
}

export function createProfileServiceMock(): jasmine.SpyObj<ProfileService> {
  return jasmine.createSpyObj<ProfileService>('ProfileService', [
    'getProfile',
    'updateProfile',
    'createPaymentPlatformAccount',
    'addPaymentPlatform',
    'removePaymentPlatform'
  ]);
}

export const validSignUpPassword = 'Password1!';

/** Doubles for page chrome and nav so specs do not construct real HTTP services. */
export function provideAppShellMocks(): Provider[] {
  const areaCounts$ = new BehaviorSubject(emptyAreaCounts());
  const unreadCount$ = new BehaviorSubject(0);
  const resourceCounts$ = new BehaviorSubject<Record<string, number>>({});

  return [
    { provide: APP_ENVIRONMENT, useValue: { production: false, apiBaseUrl: '' } },
    { provide: AuthService, useValue: createAuthServiceMock() },
    { provide: FallibleService, useValue: { recordClick: () => of(undefined) } },
    {
      provide: ClientConfigService,
      useValue: { showFallibleAttribution$: of(false), showFallibleAttribution: false }
    },
    {
      provide: NotificationService,
      useValue: {
        areaCounts$,
        unreadCount$,
        resourceCounts$,
        refreshBadges: () => undefined,
        setUnreadCount: () => undefined,
        markReadForContent: () => of({ success: true }),
        getMutes: () => of({ success: true, message: '', items: [] }),
        isMuted: () => false,
        setMute: () => of({ success: true, message: '' })
      }
    },
    {
      provide: NotificationHubService,
      useValue: { unreadCountUpdated$: EMPTY, connect: () => undefined, disconnect: () => undefined }
    },
    {
      provide: CrewService,
      useValue: {
        membershipChanged$: EMPTY,
        getMembership: () => of({ hasCrew: false }),
        getPaymentPlatforms: () => of([]),
        clearMembershipCache: () => undefined,
        clearSessionCache: () => undefined
      }
    },
    {
      provide: FleetService,
      useValue: {
        statusChanged$: EMPTY,
        getStatus: () => of({ hasFleet: false, fleetId: null, fleetName: null, imageResourceId: null })
      }
    },
    {
      provide: ProfileService,
      useValue: { getProfile: () => of({}) }
    },
    {
      provide: CryptoSessionService,
      useValue: { unlocked$: of(false), isUnlocked: () => false }
    },
    {
      provide: SecurityService,
      useValue: {
        deleteAccount: () => of({ success: true }),
        changePassword: () => of({ success: true })
      }
    },
    {
      provide: ProfileLocationService,
      useValue: {
        current: null,
        location$: of(null),
        clear: () => undefined,
        setPlaintext: () => undefined,
        encrypt: async () => null,
        decrypt: async () => null
      }
    },
    {
      provide: EncryptedImageCacheService,
      useValue: { getDataUrl: async () => null, invalidate: () => undefined }
    },
    { provide: ProposalCryptoService, useValue: {} }
  ];
}
