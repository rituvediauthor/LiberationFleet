import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { LibraryAccessBlockReason, LibraryAccessResult, LibraryAccessService } from '../../../services/library-access.service';

@Component({
  selector: 'app-library-unlock',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './library-unlock.component.html',
  styleUrl: './library-unlock.component.css'
})
export class LibraryUnlockComponent implements OnInit {
  backButton!: ActionBarButton;
  reason: LibraryAccessBlockReason = 'season-not-started';
  title = 'Library of Things locked';
  message = 'Start or join a season of giving to unlock Library of Things functions.';
  primaryLabel = 'Set up season';
  secondaryLabel: string | null = 'Join season';

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private libraryAccess = inject(LibraryAccessService);

  ngOnInit() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew'])
    };

    const reason = this.route.snapshot.queryParamMap.get('reason') as LibraryAccessBlockReason | null;
    if (reason) {
      this.applyReason(reason);
      return;
    }

    this.libraryAccess.resolveAccess().subscribe({
      next: (access: LibraryAccessResult) => {
        if (access.allowed) {
          this.router.navigate(['/app/crew/library-of-things']);
          return;
        }

        this.applyReason(access.reason);
      }
    });
  }

  goPrimary() {
    if (this.reason === 'disabled') {
      this.router.navigate(['/app/crew']);
      return;
    }

    if (this.reason === 'not-in-season') {
      this.router.navigate(['/app/crew/join-season']);
      return;
    }

    this.router.navigate(['/app/crew/season-setup']);
  }

  goSecondary() {
    this.router.navigate(['/app/crew/join-season']);
  }

  private applyReason(reason: LibraryAccessBlockReason) {
    this.reason = reason;
    switch (reason) {
      case 'disabled':
        this.title = 'Library of Things disabled';
        this.message = 'Your crew has turned off Library of Things. Ask a crewmate to enable it in crew settings if you want to use it.';
        this.primaryLabel = 'Back to crew home';
        this.secondaryLabel = null;
        break;
      case 'not-in-season':
        this.title = 'Join the season';
        this.message = 'Join the current season of giving to unlock Library of Things functions.';
        this.primaryLabel = 'Join season';
        this.secondaryLabel = null;
        break;
      default:
        this.title = 'Season not started';
        this.message = 'Start or join a season of giving to unlock Library of Things functions.';
        this.primaryLabel = 'Set up season';
        this.secondaryLabel = 'Join season';
        break;
    }
  }
}
