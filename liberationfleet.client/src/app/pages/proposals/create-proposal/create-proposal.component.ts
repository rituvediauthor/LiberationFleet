import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ProposalAttachmentPickerComponent } from '../../../components/proposal-attachment-picker/proposal-attachment-picker.component';
import { ProposalService } from '../../../services/proposal.service';
import { ProposalCryptoService } from '../../../services/crypto/proposal-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ProfileService } from '../../../services/profile.service';
import { ToastService } from '../../../components/toast/toast.component';
import { PendingAttachment } from '../../../models/proposal.model';

@Component({
  selector: 'app-create-proposal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent, ProposalAttachmentPickerComponent],
  templateUrl: './create-proposal.component.html',
  styleUrl: './create-proposal.component.css'
})
export class CreateProposalComponent implements OnInit {
  form!: FormGroup;
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  attachments: PendingAttachment[] = [];
  isSubmitting = false;
  crewId = 0;
  authorDisplayName = '';

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private proposalService = inject(ProposalService);
  private proposalCrypto = inject(ProposalCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', [Validators.required, Validators.maxLength(10000)]]
    });

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/proposals'])
    };

    this.updateCreateButton();

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
      }
    });

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.authorDisplayName = profile.username;
      }
    });

    this.form.statusChanges.subscribe(() => this.updateCreateButton());
    this.form.valueChanges.subscribe(() => this.updateCreateButton());
  }

  onSubmit() {
    if (this.form.invalid || this.isSubmitting || this.crewId <= 0) {
      return;
    }

    this.isSubmitting = true;
    this.updateCreateButton();

    const { title, description } = this.form.getRawValue();
    this.proposalCrypto.encryptProposalPayload(
      this.crewId,
      {
        title: title.trim(),
        description: description.trim(),
        authorDisplayName: 'Anonymous'
      },
      this.attachments
    ).then(encrypted => {
      this.proposalService.createProposal(encrypted).subscribe({
        next: result => {
          if (result.success) {
            this.toastService.success(result.message || 'Proposal created');
            this.router.navigate(['/app/crew/proposals/list/pending']);
            return;
          }
          this.toastService.error(result.message || 'Failed to create proposal');
          this.isSubmitting = false;
          this.updateCreateButton();
        },
        error: err => {
          this.toastService.error(err?.error?.message || 'Failed to create proposal');
          this.isSubmitting = false;
          this.updateCreateButton();
        }
      });
    }).catch(() => {
      this.toastService.error('Failed to encrypt proposal content.');
      this.isSubmitting = false;
      this.updateCreateButton();
    });
  }

  private updateCreateButton() {
    this.createButton = {
      label: 'Create',
      type: 'primary',
      disabled: this.isSubmitting || this.form.invalid,
      onClick: () => this.onSubmit()
    };
  }
}
