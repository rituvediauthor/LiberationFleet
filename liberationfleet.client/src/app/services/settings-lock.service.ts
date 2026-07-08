import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SecurityService } from './security.service';

@Injectable({
  providedIn: 'root'
})
export class SettingsLockService {
  private lockEnabled: boolean | null = null;

  constructor(private securityService: SecurityService) {}

  async isLockEnabled(): Promise<boolean> {
    if (this.lockEnabled !== null) {
      return this.lockEnabled;
    }

    try {
      const response = await firstValueFrom(this.securityService.getSettings());
      this.lockEnabled = !!(response.success && response.settings?.lockSettingsWithPassword);
      return this.lockEnabled;
    } catch {
      return false;
    }
  }

  refreshLockState(): void {
    this.lockEnabled = null;
  }

  async verifyPassword(password: string): Promise<{ success: boolean; message: string }> {
    try {
      const response = await firstValueFrom(this.securityService.verifySettingsPassword({ settingsPassword: password }));
      return { success: response.success, message: response.message };
    } catch {
      return { success: false, message: 'Failed to verify settings password.' };
    }
  }
}
