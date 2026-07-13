import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { FleetService } from '../../../services/fleet.service';
import { ToastService } from '../../../components/toast/toast.component';
import { FleetRule } from '../../../models/fleet.model';

@Component({
  selector: 'app-fleet-rule-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './fleet-rule-list.component.html',
  styleUrl: './fleet-rule-list.component.css'
})
export class FleetRuleListComponent implements OnInit {
  rules: FleetRule[] = [];
  loading = true;
  errorMessage = '';
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;

  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/fleet']);

    this.createButton = {
      label: 'Create Rule',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/fleet/rules/create'])
    };

    this.loadRules();
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
