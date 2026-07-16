import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs/operators';
import { ToastContainerComponent } from './components/toast/toast.component';
import { DevToolbarComponent } from './components/dev-toolbar/dev-toolbar.component';
import { CryptoUnlockDialogComponent } from './components/crypto-unlock-dialog/crypto-unlock-dialog.component';
import { AuthService } from './services/auth.service';
import { CryptoSessionService } from './services/crypto/crypto-session.service';
import { CrewCryptoSyncService } from './services/crew-crypto-sync.service';
import { FleetCryptoSyncService } from './services/fleet-crypto-sync.service';
import { NotificationHubService } from './services/notification-hub.service';
import { NotificationService } from './services/notification.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, ToastContainerComponent, DevToolbarComponent, CryptoUnlockDialogComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  showDevToolbar = false;
  showCryptoUnlock = false;

  private authService = inject(AuthService);
  private cryptoSession = inject(CryptoSessionService);
  private crewCryptoSync = inject(CrewCryptoSyncService);
  private fleetCryptoSync = inject(FleetCryptoSyncService);
  private notificationHub = inject(NotificationHubService);
  private notificationService = inject(NotificationService);
  private router = inject(Router);

  ngOnInit() {
    void this.authService.getEncryptionReady().then(() => {
      this.syncUnlockDialog();
      void this.syncCrewCryptoIfInApp();
      void this.syncFleetCryptoIfInApp();
      void this.connectNotificationsIfInApp();
    });

    this.cryptoSession.unlocked$.subscribe(unlocked => {
      this.syncUnlockDialog();
      if (unlocked) {
        void this.syncCrewCryptoIfInApp();
        void this.syncFleetCryptoIfInApp();
      }
    });

    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd)
    ).subscribe(() => {
      this.syncUnlockDialog();
      void this.connectNotificationsIfInApp();
      this.focusMainContent();
    });
  }

  onCryptoUnlocked() {
    this.syncUnlockDialog();
    void this.syncCrewCryptoIfInApp();
    void this.syncFleetCryptoIfInApp();
  }

  private focusMainContent() {
    if (typeof document === 'undefined') {
      return;
    }
    // Don't steal focus from skip-link or within an open dialog.
    const active = document.activeElement as HTMLElement | null;
    if (active?.classList.contains('skip-link')) {
      return;
    }
    if (active?.closest('[aria-modal="true"]')) {
      return;
    }
    const main = document.getElementById('main-content');
    main?.focus({ preventScroll: true });
  }

  private syncUnlockDialog() {
    const inAuthenticatedApp = this.router.url.startsWith('/app');
    this.showCryptoUnlock = inAuthenticatedApp && this.authService.needsEncryptionUnlock();
  }

  private syncCrewCryptoIfInApp() {
    if (!this.router.url.startsWith('/app')) {
      return;
    }
    void this.crewCryptoSync.syncActiveCrewKeyDistributions();
  }

  private syncFleetCryptoIfInApp() {
    if (!this.router.url.startsWith('/app')) {
      return;
    }
    void this.fleetCryptoSync.syncActiveFleetKeyDistributions();
  }

  private connectNotificationsIfInApp() {
    if (!this.router.url.startsWith('/app') || !this.authService.getToken()) {
      return;
    }

    void this.notificationHub.connect();
    this.notificationService.refreshBadges();
    if (typeof Notification !== 'undefined' && Notification.permission === 'default') {
      void Notification.requestPermission();
    }
  }
}
