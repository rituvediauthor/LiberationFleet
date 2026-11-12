import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { ConfirmDialogComponent } from '../../components/confirm-dialog/confirm-dialog.component';
import { NavigationService } from '../../services/navigation.service';
import { AccountRecoveryService } from '../../services/account-recovery.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../components/toast/toast.component';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-account-recovery',
  standalone: true,
  imports: [CommonModule, FormsModule, PageLayoutComponent, ConfirmDialogComponent],
  templateUrl: './account-recovery.component.html',
  styleUrl: './account-recovery.component.css'
})
export class AccountRecoveryComponent implements OnInit {
  oldUserId = 0;
  oldUsername = '';
  maskedEmail = '';
  challengeToken = '';
  password = '';
  code = '';
  loading = false;
  sendingCode = false;
  errorMessage = '';
  showKeyNotice = false;
  keyNoticeMessage = '';
  backButton!: ActionBarButton;
  recoverButton!: ActionBarButton;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private navigation: NavigationService,
    private accountRecovery: AccountRecoveryService,
    private auth: AuthService,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.oldUserId = Number(this.route.snapshot.paramMap.get('oldUserId'));
    this.backButton = this.navigation.createBackButton(['/app/crew/crewmates', String(this.oldUserId)]);
    this.recoverButton = {
      label: 'Recover',
      type: 'primary',
      onClick: () => void this.onRecover(),
      disabled: false
    };

    if (!this.oldUserId) {
      this.errorMessage = 'Invalid account.';
      return;
    }

    void this.sendCode();
  }

  async sendCode(): Promise<void> {
    if (this.sendingCode || !this.oldUserId) {
      return;
    }

    this.sendingCode = true;
    this.errorMessage = '';
    try {
      const response = await firstValueFrom(
        this.challengeToken
          ? this.accountRecovery.resend(this.challengeToken)
          : this.accountRecovery.begin(this.oldUserId)
      );
      if (!response.success || !response.challengeToken) {
        this.errorMessage = response.message || 'Could not send a recovery code.';
        return;
      }

      this.challengeToken = response.challengeToken;
      if (response.maskedEmail) {
        this.maskedEmail = response.maskedEmail;
      }
      if (response.oldUsername) {
        this.oldUsername = response.oldUsername;
      }
      this.toast.show(response.message || 'Recovery code sent.');
    } catch {
      this.errorMessage = 'Could not send a recovery code. Try again.';
    } finally {
      this.sendingCode = false;
    }
  }

  async onRecover(): Promise<void> {
    if (this.loading) {
      return;
    }

    const password = this.password.trim();
    const code = this.code.trim();
    if (!password || !code || !this.challengeToken) {
      this.errorMessage = 'Enter the old account password and the email recovery code.';
      return;
    }

    this.loading = true;
    this.recoverButton = { ...this.recoverButton, disabled: true };
    this.errorMessage = '';
    try {
      const response = await firstValueFrom(
        this.accountRecovery.complete(this.oldUserId, this.challengeToken, code, password)
      );
      if (!response.success || !response.token) {
        this.errorMessage = response.message || 'Recovery failed.';
        return;
      }

      const absorbedUsername = (response.absorbedUsername || 'your previous login').trim();
      const recoveredUsername = (response.recoveredUsername || this.oldUsername || 'this account').trim();
      this.keyNoticeMessage =
        `${recoveredUsername} now uses the 12-word key that ${absorbedUsername} used for encryption. ` +
        `Please make sure you hold onto that key so that you can access your account in the future.`;

      await this.auth.establishSessionPreservingCrypto(response);
      this.toast.show(response.message || 'Account recovered.');
      this.showKeyNotice = true;
    } catch {
      this.errorMessage = 'Recovery failed. Check the password and code, then try again.';
    } finally {
      this.loading = false;
      this.recoverButton = { ...this.recoverButton, disabled: false };
    }
  }

  async onKeyNoticeClosed(): Promise<void> {
    this.showKeyNotice = false;
    await this.router.navigateByUrl('/app/crew');
  }
}
