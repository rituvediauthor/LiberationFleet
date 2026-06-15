import { of } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { UserService } from '../services/user.service';
import { CrewService } from '../services/crew.service';
import { ToastService } from '../components/toast/toast.component';

export function createAuthServiceMock(): jasmine.SpyObj<AuthService> {
  return jasmine.createSpyObj<AuthService>('AuthService', [
    'login',
    'logout',
    'establishSession',
    'getToken',
    'setToken',
    'removeToken',
    'isAuthenticated'
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

export const validSignUpPassword = 'Password1!';
