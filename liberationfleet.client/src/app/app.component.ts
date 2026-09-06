import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { ToastContainerComponent } from './components/toast/toast.component';
import { DevToolbarComponent } from './components/dev-toolbar/dev-toolbar.component';
import { CryptoUnlockDialogComponent } from './components/crypto-unlock-dialog/crypto-unlock-dialog.component';
import { MediaUploadProgressComponent } from './components/media-upload-progress/media-upload-progress.component';
import { AuthService } from './services/auth.service';
import { CrewService } from './services/crew.service';
import { CryptoSessionService } from './services/crypto/crypto-session.service';
import { CrewCryptoSyncService } from './services/crew-crypto-sync.service';
import { FleetCryptoSyncService } from './services/fleet-crypto-sync.service';
import { NotificationHubService } from './services/notification-hub.service';
import { NotificationService } from './services/notification.service';
import { NotificationItem } from './models/notification.model';
import {
  isCrewDashboardRedirectUrl,
  isCrewJoinRequestApprovedNotification
} from './utils/crew-join-approval.util';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    ToastContainerComponent,
    DevToolbarComponent,
    CryptoUnlockDialogComponent,
    MediaUploadProgressComponent
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit, OnDestroy {
  showDevToolbar = false;
  showCryptoUnlock = false;

  private authService = inject(AuthService);
  private crewService = inject(CrewService);
  private cryptoSession = inject(CryptoSessionService);
  private crewCryptoSync = inject(CrewCryptoSyncService);
  private fleetCryptoSync = inject(FleetCryptoSyncService);
  private notificationHub = inject(NotificationHubService);
  private notificationService = inject(NotificationService);
  private router = inject(Router);
  private notificationsBootstrapped = false;
  private readonly subscriptions = new Subscription();

  ngOnInit() {
    void this.authService.getEncryptionReady().then(() => {
      this.syncUnlockDialog();
      void this.syncCrewCryptoIfInApp();
      void this.syncFleetCryptoIfInApp();
      void this.connectNotificationsIfInApp();
    });

    this.subscriptions.add(
      this.cryptoSession.unlocked$.subscribe(unlocked => {
        this.syncUnlockDialog();
        if (unlocked) {
          void this.syncCrewCryptoIfInApp();
          void this.syncFleetCryptoIfInApp();
        }
      })
    );

    this.subscriptions.add(
      this.router.events.pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd)
      ).subscribe(() => {
        this.syncUnlockDialog();
        void this.connectNotificationsIfInApp();
        this.focusMainContent();
      })
    );

    this.subscriptions.add(
      this.notificationHub.notificationReceived$.subscribe(notification => {
        this.onJoinRequestApproved(notification);
      })
    );
  }

  ngOnDestroy() {
    this.subscriptions.unsubscribe();
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
    if (!inAuthenticatedApp || !this.authService.isAuthenticated()) {
      this.showCryptoUnlock = false;
      return;
    }

    // Wait until local session/device unlock has been tried before showing the dialog.
    void this.authService.getEncryptionReady().then(() => {
      this.showCryptoUnlock =
        this.router.url.startsWith('/app') && this.authService.needsEncryptionUnlock();
    });
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
      this.notificationsBootstrapped = false;
      return;
    }

    void this.notificationHub.connect();
    if (!this.notificationsBootstrapped) {
      this.notificationsBootstrapped = true;
      this.notificationService.refreshBadges(true);
      if (typeof Notification !== 'undefined' && Notification.permission === 'default') {
        void Notification.requestPermission();
      }
    }
  }

  /**
   * When a pending join request is approved while the user is signed in with no crew,
   * drop the stale membership cache so Crew Home shows the dashboard. If they are already
   * on a no-crew crew surface, navigate there immediately.
   */
  private onJoinRequestApproved(notification: NotificationItem) {
    if (!isCrewJoinRequestApprovedNotification(notification)) {
      return;
    }

    this.crewService.clearMembershipCache();
    void this.crewCryptoSync.syncActiveCrewKeyDistributions();

    if (isCrewDashboardRedirectUrl(this.router.url)) {
      void this.router.navigate(['/app/crew'], { replaceUrl: true });
    }
  }
}
