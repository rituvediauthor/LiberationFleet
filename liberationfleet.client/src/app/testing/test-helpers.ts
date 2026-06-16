import { of } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { UserService } from '../services/user.service';
import { CrewService } from '../services/crew.service';
import { GiftService } from '../services/gift.service';
import { ProfileService } from '../services/profile.service';
import { ToastService } from '../components/toast/toast.component';

export function createAuthServiceMock(): jasmine.SpyObj<AuthService> {
  return jasmine.createSpyObj<AuthService>('AuthService', [
    'login',
    'logout',
    'establishSession',
    'getToken',
    'setToken',
    'removeToken',
    'isAuthenticated',
    'updateCurrentUser'
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

export function clearAuthStorage(): void {
  localStorage.removeItem('auth_token');
}

export function createCrewServiceMock(): jasmine.SpyObj<CrewService> {
  return jasmine.createSpyObj<CrewService>('CrewService', [
    'getMembership',
    'create',
    'search',
    'join'
  ]);
}

export function createGiftServiceMock(): jasmine.SpyObj<GiftService> {
  const mock = jasmine.createSpyObj<GiftService>('GiftService', [
    'getNextAidInfo',
    'getCrewMembers',
    'getPaymentPlatforms',
    'getPendingMiddlemanGifts',
    'getLogs',
    'isUserRelated',
    'recordGift'
  ]);
  mock.getCrewMembers.and.returnValue(of([]));
  mock.getPaymentPlatforms.and.returnValue(of([
    { id: 1, name: 'PayPal' },
    { id: 2, name: 'Cash App' },
    { id: 3, name: 'Venmo' },
    { id: 4, name: 'Zelle' },
    { id: 5, name: 'Other' }
  ]));
  mock.getPendingMiddlemanGifts.and.returnValue(of([]));
  mock.getLogs.and.returnValue(of([]));
  mock.recordGift.and.returnValue(of({ success: true, message: 'Gift recorded' }));
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
