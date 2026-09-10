import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { App } from '@capacitor/app';
import type { PluginListenerHandle } from '@capacitor/core';
import { Subscription, firstValueFrom, of } from 'rxjs';
import { catchError, filter } from 'rxjs/operators';
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
  isCrewJoinRequestApprovedNotification,
  isNewSeasonNotification
} from './utils/crew-join-approval.util';
import { isNativeApp } from './utils/app-platform.util';

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
  private appStateListener: PluginListenerHandle | null = null;
  private readonly onVisibilityChange = () => {
    if (document.visibilityState === 'visible') {
      this.refreshMembershipIfInApp();
    }
  };

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
        this.onMembershipAffectingNotification(notification);
      })
    );

    // Capacitor / backgrounded tabs often miss the live join-approval SignalR event.
    // Re-check membership when the app returns to the foreground.
    if (typeof document !== 'undefined') {
      document.addEventListener('visibilitychange', this.onVisibilityChange);
    }
    if (isNativeApp()) {
      void App.addListener('appStateChange', ({ isActive }) => {
        if (isActive) {
          this.refreshMembershipIfInApp();
        }
      }).then(handle => {
        this.appStateListener = handle;
      });
    }
  }

  ngOnDestroy() {
    this.subscriptions.unsubscribe();
    if (typeof document !== 'undefined') {
      document.removeEventListener('visibilitychange', this.onVisibilityChange);
    }
    void this.appStateListener?.remove();
    this.appStateListener = null;
  }

  onCryptoUnlocked() {
    this.syncUnlockDialog();
    void this.syncCrewCryptoIfInApp();
    void this.syncFleetCryptoIfInApp();
  }

  private refreshMembershipIfInApp() {
    if (!this.router.url.startsWith('/app') || !this.authService.getToken()) {
      return;
    }

    void this.recoverMembershipAfterForeground();
  }

  /**
   * Backgrounded tabs often miss the live join-approval SignalR event.
   * On resume, refetch membership and land on the crew dashboard if we just joined.
   */
  private async recoverMembershipAfterForeground() {
    void this.connectNotificationsIfInApp();

    const previous = await firstValueFrom(
      this.crewService.getMembership().pipe(catchError(() => of(null)))
    ).catch(() => null);

    this.crewService.clearMembershipCache();

    const next = await firstValueFrom(
      this.crewService.getMembership(true).pipe(catchError(() => of(null)))
    ).catch(() => null);

    const joinedWhileAway = !previous?.hasCrew && !!next?.hasCrew;
    if (joinedWhileAway && this.router.url.startsWith('/app')) {
      void this.router.navigate(['/app/crew'], { replaceUrl: true });
    }
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
   * Live notifications that invalidate session membership (join approved, season started).
   * Always force-refresh membership and send the user to the right surface when relevant.
   */
  private onMembershipAffectingNotification(notification: NotificationItem) {
    if (isNewSeasonNotification(notification)) {
      this.crewService.clearMembershipCache();
      return;
    }

    if (!isCrewJoinRequestApprovedNotification(notification)) {
      return;
    }

    void this.handleJoinRequestApproved();
  }

  private async handleJoinRequestApproved() {
    this.crewService.clearMembershipCache();
    void this.crewCryptoSync.syncActiveCrewKeyDistributions();

    try {
      await firstValueFrom(
        this.crewService.getMembership(true).pipe(catchError(() => of(null)))
      );
    } catch {
      // Still navigate; crew home will retry membership load.
    }

    // Always land on the crew dashboard after acceptance (replace so back does not
    // return to join/prep surfaces that no longer apply).
    if (!this.router.url.startsWith('/app')) {
      return;
    }

    const alreadyOnCrewHome = this.router.url.split('?')[0] === '/app/crew';
    if (alreadyOnCrewHome) {
      // Same-URL navigate is a no-op; membershipChanged$ already refreshed crew-home.
      return;
    }

    void this.router.navigate(['/app/crew'], { replaceUrl: true });
  }
}
