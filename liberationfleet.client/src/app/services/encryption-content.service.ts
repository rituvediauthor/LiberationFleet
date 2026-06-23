import { Injectable, inject } from '@angular/core';
import { Subscription, filter } from 'rxjs';
import { AuthService } from './auth.service';
import { CryptoSessionService } from './crypto/crypto-session.service';

export interface EncryptionReloadHandle {
  subscription: Subscription;
  markInitialLoadDone: () => void;
}

@Injectable({
  providedIn: 'root'
})
export class EncryptionContentService {
  private authService = inject(AuthService);
  private cryptoSession = inject(CryptoSessionService);

  async whenReady(): Promise<void> {
    await this.authService.getEncryptionReady();
  }

  watchForUnlockAfterInitialLoad(reload: () => void): EncryptionReloadHandle {
    let initialLoadDone = false;
    const subscription = this.cryptoSession.unlocked$.pipe(
      filter(unlocked => unlocked && initialLoadDone)
    ).subscribe(() => reload());

    return {
      subscription,
      markInitialLoadDone: () => {
        initialLoadDone = true;
      }
    };
  }
}
