import { AfterViewChecked, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { NotificationContentService } from '../../../services/notification-content.service';
import { NotificationTargetDirective } from '../../../directives/notification-target.directive';
import { FleetRule } from '../../../models/fleet.model';
import {
  clearNotificationHighlightParams,
  readNotificationHighlightId
} from '../../../utils/notification-deep-link.util';

@Component({
  selector: 'app-fleet-rule-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent, NotificationTargetDirective],
  templateUrl: './fleet-rule-list.component.html',
  styleUrl: './fleet-rule-list.component.css'
})
export class FleetRuleListComponent implements OnInit, AfterViewChecked {
  rules: FleetRule[] = [];
  loading = true;
  errorMessage = '';
  highlightId: number | null = null;
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;

  private readonly notificationPrefix = '/app/fleet/rules';
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);
  private notificationContent = inject(NotificationContentService);
  private markedListVisit = false;

  ngOnInit() {
    this.highlightId = readNotificationHighlightId(this.route);
    clearNotificationHighlightParams(this.router, this.route);

    this.backButton = this.navigation.createBackButton(['/app/fleet']);

    this.createButton = {
      label: 'Create Rule',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/fleet/rules/create'])
    };

    this.loadRules();
  }

  ngAfterViewChecked() {
    if (!this.markedListVisit && !this.loading) {
      this.markedListVisit = true;
      this.notificationContent.markVisited(this.notificationPrefix);
    }
  }

  get notifyPrefix(): string {
    return this.notificationPrefix;
  }

  editRule(rule: FleetRule) {
    this.router.navigate(['/app/fleet/rules', rule.id, 'edit']);
  }

  private loadRules() {
    this.loading = true;
    this.errorMessage = '';
    this.fleetService.getRules().subscribe({
      next: response => {
        this.loading = false;
        if (!response.success) {
          this.errorMessage = response.message || 'Failed to load rules';
          return;
        }
        this.rules = response.items ?? [];
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load rules';
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
