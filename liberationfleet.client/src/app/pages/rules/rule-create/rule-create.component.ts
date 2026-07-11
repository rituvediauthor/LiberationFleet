import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { RuleService } from '../../../services/rule.service';
import { RuleCryptoService } from '../../../services/crypto/rule-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { ToastService } from '../../../components/toast/toast.component';

@Component({
  selector: 'app-rule-create',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './rule-create.component.html',
  styleUrl: './rule-create.component.css'
})
export class RuleCreateComponent implements OnInit {
  form!: FormGroup;
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  isSubmitting = false;
  crewId = 0;
  requireApprovalForEdits = true;

  private fb = inject(FormBuilder);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private ruleService = inject(RuleService);
  private ruleCrypto = inject(RuleCryptoService);
  private crewService = inject(CrewService);
  private encryptionContent = inject(EncryptionContentService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', [Validators.required, Validators.maxLength(10000)]],
      isPublic: [false]
    });

    this.backButton = this.navigation.createBackButton(['/app/crew/rules']);

    this.updateCreateButton();

    this.crewService.getCurrentCrew().subscribe({
      next: result => {
        if (result.success && result.crew) {
          this.crewId = result.crew.id;
          this.requireApprovalForEdits = result.crew.requireApprovalForEdits ?? true;
          this.updateCreateButton();
        }
      }
    });

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? this.crewId;
      }
    });

    this.form.statusChanges.subscribe(() => this.updateCreateButton());
    this.form.valueChanges.subscribe(() => this.updateCreateButton());
  }

  async onSubmit() {
    if (this.form.invalid || this.isSubmitting || this.crewId <= 0) {
      return;
    }

    this.isSubmitting = true;
    this.updateCreateButton();

    try {
      await this.encryptionContent.whenReady();
      const { title, description, isPublic } = this.form.getRawValue();

      if (isPublic) {
        this.ruleService.createRule({
          isPublic: true,
          nonce: '',
          ciphertext: '',
          plaintextTitle: title.trim(),
          plaintextDescription: description.trim()
        }).subscribe({
          next: result => this.handleCreateResult(result),
          error: err => this.handleCreateError(err)
        });
        return;
      }

      const encrypted = await this.ruleCrypto.encryptRulePayload(this.crewId, {
        title: title.trim(),
        description: description.trim(),
        authorDisplayName: 'Anonymous'
      });

      this.ruleService.createRule({
        ...encrypted,
        plaintextTitle: title.trim(),
        plaintextDescription: description.trim()
      }).subscribe({
        next: result => this.handleCreateResult(result),
        error: err => this.handleCreateError(err)
      });
    } catch {
      this.toastService.error('Failed to encrypt rule content');
      this.isSubmitting = false;
      this.updateCreateButton();
    }
  }

  private handleCreateResult(result: { success: boolean; message?: string; proposalsSubmitted?: boolean }) {
    if (result.success && result.proposalsSubmitted) {
      this.toastService.success(result.message || 'Proposal submitted for crew approval');
      this.router.navigate(['/app/crew/proposals/list/pending']);
      return;
    }
    if (result.success) {
      this.toastService.success('Rule created');
      this.router.navigate(['/app/crew/rules']);
      return;
    }
    this.toastService.error(result.message || 'Failed to create rule');
    this.isSubmitting = false;
    this.updateCreateButton();
  }

  private handleCreateError(err: { error?: { message?: string } }) {
    this.toastService.error(err?.error?.message || 'Failed to create rule');
    this.isSubmitting = false;
    this.updateCreateButton();
  }

  private updateCreateButton() {
    this.createButton = {
      label: this.requireApprovalForEdits ? 'Submit proposal' : 'Create',
      type: 'primary',
      disabled: this.isSubmitting || this.form.invalid,
      onClick: () => void this.onSubmit()
    };
  }
}
