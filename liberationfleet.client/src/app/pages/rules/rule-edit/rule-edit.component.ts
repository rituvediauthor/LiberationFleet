import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { RuleService } from '../../../services/rule.service';
import { RuleCryptoService } from '../../../services/crypto/rule-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { ToastService } from '../../../components/toast/toast.component';
import { isSaveActionDisabled } from '../../../utils/save-button.util';
import { isControlInvalidForA11y } from '../../../utils/a11y-form.util';

@Component({
  selector: 'app-rule-edit',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './rule-edit.component.html',
  styleUrl: './rule-edit.component.css'
})
export class RuleEditComponent implements OnInit {
  form!: FormGroup;
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;
  isSubmitting = false;
  isDeleting = false;
  loading = true;
  loadError = '';
  crewId = 0;
  ruleId = 0;
  requireApprovalForEdits = true;
  private initialFormValues: { title: string; description: string; isPublic: boolean } | null = null;

  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private ruleService = inject(RuleService);
  private ruleCrypto = inject(RuleCryptoService);
  private crewService = inject(CrewService);
  private encryptionContent = inject(EncryptionContentService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.ruleId = Number(this.route.snapshot.paramMap.get('id'));

    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', [Validators.required, Validators.maxLength(10000)]],
      isPublic: [false]
    });

    this.backButton = this.navigation.createBackButton(['/app/crew/rules']);

    this.updateSaveButton();

    this.crewService.getCurrentCrew().subscribe({
      next: result => {
        if (result.success && result.crew) {
          this.crewId = result.crew.id;
          this.requireApprovalForEdits = result.crew.requireApprovalForEdits ?? true;
          this.updateSaveButton();
        }
      }
    });

    this.crewService.getMembership().subscribe({
      next: async membership => {
        this.crewId = membership.crewId ?? this.crewId;
        await this.encryptionContent.whenReady();
        this.loadRule();
      },
      error: () => {
        this.loading = false;
        this.loadError = 'Failed to load crew membership';
      }
    });

    this.form.statusChanges.subscribe(() => this.updateSaveButton());
    this.form.valueChanges.subscribe(() => this.updateSaveButton());
  }

  get saveButtonLabel(): string {
    return this.requireApprovalForEdits ? 'Submit proposal' : 'Save';
  }

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form?.get(controlName));
  }

  async onSubmit() {
    if (this.form.invalid || this.isSubmitting || this.crewId <= 0) {
      return;
    }

    this.isSubmitting = true;
    this.updateSaveButton();

    try {
      const { title, description, isPublic } = this.form.getRawValue();
      const oldValues = this.initialFormValues ?? { title: '', description: '', isPublic: false };

      if (isPublic) {
        this.ruleService.updateRule(this.ruleId, {
          isPublic: true,
          nonce: '',
          ciphertext: '',
          plaintextTitle: title.trim(),
          plaintextDescription: description.trim(),
          plaintextOldTitle: oldValues.title,
          plaintextOldDescription: oldValues.description
        }).subscribe({
          next: result => this.handleSaveResult(result),
          error: err => this.handleSaveError(err)
        });
        return;
      }

      const encrypted = await this.ruleCrypto.encryptRulePayload(this.crewId, {
        title: title.trim(),
        description: description.trim(),
        authorDisplayName: 'Anonymous'
      });

      this.ruleService.updateRule(this.ruleId, {
        ...encrypted,
        plaintextTitle: title.trim(),
        plaintextDescription: description.trim(),
        plaintextOldTitle: oldValues.title,
        plaintextOldDescription: oldValues.description
      }).subscribe({
        next: result => this.handleSaveResult(result),
        error: err => this.handleSaveError(err)
      });
    } catch {
      this.toastService.error('Failed to encrypt rule content');
      this.isSubmitting = false;
      this.updateSaveButton();
    }
  }

  private handleSaveResult(result: { success: boolean; message?: string; proposalsSubmitted?: boolean }) {
    if (result.success && result.proposalsSubmitted) {
      this.toastService.success(result.message || 'Proposal submitted for crew approval');
      this.router.navigate(['/app/crew/proposals/list/pending']);
      return;
    }
    if (result.success) {
      this.toastService.success('Rule saved');
      this.router.navigate(['/app/crew/rules']);
      return;
    }
    this.toastService.error(result.message || 'Failed to save rule');
    this.isSubmitting = false;
    this.updateSaveButton();
  }

  private handleSaveError(err: { error?: { message?: string } }) {
    this.toastService.error(err?.error?.message || 'Failed to save rule');
    this.isSubmitting = false;
    this.updateSaveButton();
  }

  deleteRule() {
    if (this.isDeleting || this.isSubmitting) {
      return;
    }

    const values = this.initialFormValues ?? this.form.getRawValue();
    this.isDeleting = true;
    this.ruleService.deleteRule(this.ruleId, {
      plaintextTitle: String(values.title).trim(),
      plaintextDescription: String(values.description).trim()
    }).subscribe({
      next: result => {
        this.isDeleting = false;
        if (result.success && result.proposalsSubmitted) {
          this.toastService.success(result.message || 'Proposal submitted for crew approval');
          this.router.navigate(['/app/crew/proposals/list/pending']);
          return;
        }
        if (result.success) {
          this.toastService.success('Rule deleted');
          this.router.navigate(['/app/crew/rules']);
          return;
        }
        this.toastService.error(result.message || 'Failed to delete rule');
      },
      error: () => {
        this.isDeleting = false;
        this.toastService.error('Failed to delete rule');
      }
    });
  }

  private loadRule() {
    this.loading = true;
    this.loadError = '';
    this.ruleService.getRule(this.ruleId).subscribe({
      next: async response => {
        try {
          if (!response.success || !response.rule) {
            this.loadError = response.message || 'Failed to load rule';
            return;
          }
          const decrypted = this.crewId > 0
            ? await this.ruleCrypto.decryptDetail(response.rule, this.crewId)
            : response.rule;
          this.form.patchValue({
            title: decrypted.title ?? '',
            description: decrypted.description ?? '',
            isPublic: decrypted.isPublic ?? false
          });
          this.initialFormValues = this.form.getRawValue();
        } finally {
          this.loading = false;
          this.updateSaveButton();
        }
      },
      error: () => {
        this.loading = false;
        this.loadError = 'Failed to load rule';
        this.toastService.error(this.loadError);
      }
    });
  }

  private updateSaveButton() {
    this.saveButton = {
      label: this.saveButtonLabel,
      type: 'primary',
      disabled: isSaveActionDisabled({
        form: this.form,
        initialValues: this.initialFormValues,
        isLoading: this.loading,
        isSaving: this.isSubmitting
      }),
      onClick: () => void this.onSubmit()
    };
  }
}
