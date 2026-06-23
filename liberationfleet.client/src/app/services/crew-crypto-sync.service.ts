import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CrewService } from './crew.service';
import { CryptoSessionService } from './crypto/crypto-session.service';

@Injectable({
  providedIn: 'root'
})
export class CrewCryptoSyncService {
  private crewService = inject(CrewService);
  private cryptoSession = inject(CryptoSessionService);

  async syncActiveCrewKeyDistributions(): Promise<void> {
    if (!this.cryptoSession.isUnlocked()) {
      return;
    }

    const membership = await firstValueFrom(this.crewService.getMembership());
    if (!membership.hasCrew || !membership.crewId) {
      return;
    }

    await this.cryptoSession.syncCrewKeyDistributions(membership.crewId);
  }

  async prepareNewMemberAccess(crewId: number): Promise<void> {
    if (!this.cryptoSession.isUnlocked()) {
      return;
    }

    try {
      await this.cryptoSession.ensureCrewKeyReady(crewId);
    } catch {
      // New members may need an existing crewmate to open the app first.
    }
  }
}
