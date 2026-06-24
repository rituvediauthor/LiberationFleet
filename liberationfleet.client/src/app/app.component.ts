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
  private router = inject(Router);

  ngOnInit() {
    void this.authService.getEncryptionReady().then(() => {
      this.syncUnlockDialog();
      void this.syncCrewCryptoIfInApp();
    });

    this.cryptoSession.unlocked$.subscribe(unlocked => {
      this.syncUnlockDialog();
      if (unlocked) {
        void this.syncCrewCryptoIfInApp();
      }
    });

    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd)
    ).subscribe(() => this.syncUnlockDialog());
  }

  onCryptoUnlocked() {
    this.syncUnlockDialog();
    void this.syncCrewCryptoIfInApp();
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
}
