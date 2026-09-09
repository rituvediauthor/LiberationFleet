import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { BrandLogoComponent } from '../../components/brand-logo/brand-logo.component';
import { ConfirmDialogComponent } from '../../components/confirm-dialog/confirm-dialog.component';
import { DevMutualAidService } from '../../components/dev-toolbar/dev-mutual-aid.service';
import { DevToolsService } from '../../services/dev-tools.service';
import { ToastService } from '../../components/toast/toast.component';

@Component({
  selector: 'app-product-landing',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, BrandLogoComponent, ConfirmDialogComponent],
  templateUrl: './product-landing.component.html',
  styleUrl: './product-landing.component.css'
})
export class ProductLandingComponent implements OnInit {
  signInButton: ActionBarButton;
  showNukeDialog = false;
  nukeEnabled = false;
  nukeBusy = false;

  private readonly devMutualAidService = inject(DevMutualAidService);
  private readonly devToolsService = inject(DevToolsService);
  private readonly toastService = inject(ToastService);

  constructor(private router: Router) {
    this.signInButton = {
      label: 'Sign In',
      type: 'primary',
      onClick: () => this.navigateToSignIn()
    };
  }

  ngOnInit(): void {
    // Staging hostnames may still report ASPNETCORE_ENVIRONMENT=Production; mirror donate page.
    const isStagingHost =
      typeof location !== 'undefined' && /staging/i.test(location.hostname);

    this.devToolsService.load().subscribe({
      next: status => {
        this.nukeEnabled = status.enabled || isStagingHost;
      },
      error: () => {
        this.nukeEnabled = isStagingHost;
      }
    });
  }

  openNukeDialog(): void {
    if (this.nukeBusy || !this.nukeEnabled) {
      return;
    }

    this.showNukeDialog = true;
  }

  closeNukeDialog(): void {
    if (this.nukeBusy) {
      return;
    }

    this.showNukeDialog = false;
  }

  confirmNuke(): void {
    if (this.nukeBusy) {
      return;
    }

    this.nukeBusy = true;
    this.devMutualAidService.resetApp().subscribe({
      next: result => {
        this.nukeBusy = false;
        this.showNukeDialog = false;
        if (result.success) {
          this.toastService.success(result.message, 6000);
        } else {
          this.toastService.error(result.message, 6000);
        }
      },
      error: err => {
        this.nukeBusy = false;
        this.showNukeDialog = false;
        const message = err.error?.message || 'App reset failed.';
        this.toastService.error(message, 6000);
      }
    });
  }

  private navigateToSignIn(): void {
    this.router.navigate(['/sign-in']);
  }
}
