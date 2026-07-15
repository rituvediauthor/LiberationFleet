import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { FleetService } from './fleet.service';
import { CryptoSessionService } from './crypto/crypto-session.service';

@Injectable({
  providedIn: 'root'
})
export class FleetCryptoSyncService {
  private fleetService = inject(FleetService);
  private cryptoSession = inject(CryptoSessionService);

  async syncActiveFleetKeyDistributions(): Promise<void> {
    if (!this.cryptoSession.isUnlocked()) {
      return;
    }

    const status = await firstValueFrom(this.fleetService.getStatus());
    if (!status.hasFleet || !status.fleetId) {
      return;
    }

    await this.cryptoSession.syncFleetKeyDistributions(status.fleetId);
  }

  async prepareNewMemberAccess(fleetId: number): Promise<void> {
    if (!this.cryptoSession.isUnlocked()) {
      return;
    }

    try {
      await this.cryptoSession.ensureFleetKeyReady(fleetId);
    } catch {
      // New members may need an existing fleet member to open the app first.
    }
  }
}
