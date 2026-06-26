import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { RuleService } from '../../../services/rule.service';
import { RuleCryptoService } from '../../../services/crypto/rule-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { ToastService } from '../../../components/toast/toast.component';
import { RuleListItem } from '../../../models/rule.model';

@Component({
  selector: 'app-rule-list',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './rule-list.component.html',
  styleUrl: './rule-list.component.css'
})
export class RuleListComponent implements OnInit, OnDestroy {
  rules: RuleListItem[] = [];
  loading = true;
  errorMessage = '';
  crewId = 0;
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;

  private router = inject(Router);
  private ruleService = inject(RuleService);
  private ruleCrypto = inject(RuleCryptoService);
  private crewService = inject(CrewService);
  private encryptionContent = inject(EncryptionContentService);
  private toastService = inject(ToastService);
  private encryptionReload?: ReturnType<EncryptionContentService['watchForUnlockAfterInitialLoad']>;

  ngOnInit() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew'])
    };

    this.createButton = {
      label: 'Create Rule',
      type: 'primary',
      onClick: () => this.router.navigate(['/app/crew/rules/create'])
    };

    this.encryptionReload = this.encryptionContent.watchForUnlockAfterInitialLoad(() => this.loadRules());

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? 0;
        await this.encryptionContent.whenReady();
        this.loadRules();
        this.encryptionReload?.markInitialLoadDone();
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crew membership';
      }
    });
  }

  ngOnDestroy() {
    this.encryptionReload?.subscription.unsubscribe();
  }

  editRule(rule: RuleListItem) {
    this.router.navigate(['/app/crew/rules', rule.id, 'edit']);
  }

  private loadRules() {
    this.loading = true;
    this.errorMessage = '';
    this.ruleService.getRules().subscribe({
      next: async response => {
        try {
          if (!response.success) {
            this.errorMessage = response.message || 'Failed to load rules';
            return;
          }
          const items = response.items ?? [];
          this.rules = this.crewId > 0
            ? await this.ruleCrypto.decryptRules(items, this.crewId)
            : items;
        } finally {
          this.loading = false;
        }
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load rules';
        this.toastService.error(this.errorMessage);
      }
    });
  }
}
