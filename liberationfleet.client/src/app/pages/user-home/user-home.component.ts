import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subscription, of } from 'rxjs';
import { catchError, startWith, switchMap } from 'rxjs/operators';
import { NavLayoutComponent } from '../../components/nav-layout/nav-layout.component';
import { CrewService } from '../../services/crew.service';
import { navigateToDonate } from '../../utils/donation-nav.util';

@Component({
  selector: 'app-user-home',
  standalone: true,
  imports: [CommonModule, NavLayoutComponent],
  templateUrl: './user-home.component.html',
  styleUrl: './user-home.component.css'
})
export class UserHomeComponent implements OnInit, OnDestroy {
  hasCrew = false;

  private router = inject(Router);
  private crewService = inject(CrewService);
  private subscription = new Subscription();

  ngOnInit() {
    this.subscription.add(
      this.crewService.membershipChanged$.pipe(
        startWith(undefined),
        switchMap(() =>
          this.crewService.getMembership().pipe(
            catchError(() => of(null))
          )
        )
      ).subscribe(status => {
        this.hasCrew = !!status?.hasCrew;
      })
    );
  }

  ngOnDestroy() {
    this.subscription.unsubscribe();
  }

  goToJoinOrCreateCrew() {
    void this.router.navigate(['/app/crew/find']);
  }

  goToUserProfile() {
    this.router.navigate(['/app/profile/user']);
  }

  goToGiftHistory() {
    this.router.navigate(['/app/profile/gift-history']);
  }

  goToActivityCenter() {
    this.router.navigate(['/app/profile/activity']);
  }

  goToPreferences() {
    this.router.navigate(['/app/profile/preferences']);
  }

  goToDonate() {
    navigateToDonate(this.router);
  }
}
