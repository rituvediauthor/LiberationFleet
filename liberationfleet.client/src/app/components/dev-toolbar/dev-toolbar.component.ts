import { Component, EventEmitter, inject, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { DevMutualAidService } from './dev-mutual-aid.service';
import { ToastService } from '../toast/toast.component';
import { DevToolsService } from '../../services/dev-tools.service';
import { CrewService } from '../../services/crew.service';

interface DevToolbarAction {
  label: string;
  run: () => void;
  destructive?: boolean;
}

const COLLAPSED_STORAGE_KEY = 'dev-toolbar-collapsed';

@Component({
  selector: 'app-dev-toolbar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dev-toolbar.component.html',
  styleUrl: './dev-toolbar.component.css'
})
export class DevToolbarComponent implements OnInit {
  @Output() visibilityChange = new EventEmitter<boolean>();

  eligible = false;
  collapsed = sessionStorage.getItem(COLLAPSED_STORAGE_KEY) === 'true';
  busy = false;

  private devService = inject(DevMutualAidService);
  private devTools = inject(DevToolsService);
  private crewService = inject(CrewService);
  private router = inject(Router);
  private toastService = inject(ToastService);

  private hasCrew = false;

  readonly actions: DevToolbarAction[] = [
    { label: 'New Month', run: () => this.invoke(() => this.devService.newMonth()) },
    { label: 'New Season', run: () => this.invoke(() => this.devService.newSeason()) },
    { label: 'Complete Cycles', run: () => this.invoke(() => this.devService.completeCycles()) },
    { label: 'Recalculate Caps', run: () => this.invoke(() => this.devService.recalculateCaps()) },
    { label: 'Reset Season', run: () => this.invoke(() => this.devService.resetSeason()), destructive: true }
  ];

  ngOnInit() {
    this.devTools.load().subscribe({
      next: () => this.refreshMembership(),
      error: () => this.setEligible(false)
    });

    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(() => {
        if (this.devTools.isEnabled && isCrewMemberRoute(this.router.url)) {
          this.refreshMembership();
        } else {
          this.updateEligibility();
        }
      });
  }

  toggleCollapsed() {
    this.collapsed = !this.collapsed;
    sessionStorage.setItem(COLLAPSED_STORAGE_KEY, String(this.collapsed));
    this.emitVisibility();
  }

  private refreshMembership() {
    if (!this.devTools.isEnabled) {
      this.setEligible(false);
      return;
    }

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.hasCrew = membership.hasCrew;
        this.updateEligibility();
      },
      error: () => {
        this.hasCrew = false;
        this.updateEligibility();
      }
    });
  }

  private updateEligibility() {
    const shouldShow = this.devTools.isEnabled
      && this.hasCrew
      && isCrewMemberRoute(this.router.url);
    this.setEligible(shouldShow);
  }

  private setEligible(eligible: boolean) {
    this.eligible = eligible;
    this.emitVisibility();
  }

  private emitVisibility() {
    this.visibilityChange.emit(this.eligible && !this.collapsed);
  }

  private invoke(request: () => ReturnType<DevMutualAidService['newMonth']>) {
    if (this.busy) return;

    this.busy = true;
    request().subscribe({
      next: result => {
        this.busy = false;
        if (result.success) {
          this.toastService.success(result.message);
        } else {
          this.toastService.error(result.message);
        }
      },
      error: err => {
        this.busy = false;
        const message = err.error?.message || 'Dev action failed';
        this.toastService.error(message);
      }
    });
  }
}

function isCrewMemberRoute(url: string): boolean {
  if (!url.startsWith('/app/crew')) {
    return false;
  }

  return url !== '/app/crew/create' && url !== '/app/crew/join';
}
