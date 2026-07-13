import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ProposalAttachmentPickerComponent } from '../../../components/proposal-attachment-picker/proposal-attachment-picker.component';
import { ProposalService } from '../../../services/proposal.service';
import { ProposalCryptoService } from '../../../services/crypto/proposal-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ProfileService } from '../../../services/profile.service';
import { ToastService } from '../../../components/toast/toast.component';
import { PendingAttachment } from '../../../models/proposal.model';
import { MentionAutocompleteDirective } from '../../../directives/mention-autocomplete.directive';

@Component({
  selector: 'app-create-proposal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent, ProposalAttachmentPickerComponent, MentionAutocompleteDirective],
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
  canAttachFiles = false;
  canCreateProposals = false;
  mentionedUserIds: number[] = [];
  authorDisplayName = '';
  isFleetScope = false;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  private navigation = inject(NavigationService);
  private proposalService = inject(ProposalService);
  private proposalCrypto = inject(ProposalCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.isFleetScope = this.route.snapshot.data['scope'] === 'fleet';
    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', [Validators.required, Validators.maxLength(10000)]]
    });

    this.backButton = this.navigation.createBackButton(
      this.isFleetScope ? ['/app/fleet/proposals'] : ['/app/crew/proposals']
    );

    this.updateCreateButton();

    if (this.isFleetScope) {
      this.canCreateProposals = true;
      this.canAttachFiles = false;
      this.updateCreateButton();
    } else {
      this.crewService.getMembership().subscribe({
        next: membership => {
          this.crewId = membership.crewId ?? 0;
          this.canAttachFiles = membership.canAttachFilesToCrewContent ?? false;
          this.canCreateProposals = membership.canCreateProposals ?? false;
          this.updateCreateButton();
        }
      });
    }

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.authorDisplayName = profile.username;
      }
    });

    this.form.statusChanges.subscribe(() => this.updateCreateButton());
    this.form.valueChanges.subscribe(() => this.updateCreateButton());
  }

  onSubmit() {
    if (this.form.invalid || this.isSubmitting || !this.canCreateProposals) {
      if (!this.canCreateProposals) {
        this.toastService.error('You do not meet the requirements to create proposals.');
      }
      return;
    }

    const { title, description } = this.form.getRawValue();
    this.isSubmitting = true;
    this.updateCreateButton();

    if (this.isFleetScope) {
      this.proposalService.createFleetProposal(title.trim(), description.trim()).subscribe({
        next: result => this.handleCreateResult(result),
        error: err => this.handleCreateError(err)
      });
      return;
    }

    if (this.crewId <= 0) {
      this.isSubmitting = false;
      this.updateCreateButton();
      return;
    }

    this.proposalCrypto.encryptProposalPayload(
      this.crewId,
      {
        title: title.trim(),
        description: description.trim(),
        authorDisplayName: 'Anonymous'
      },
      this.attachments
    ).then(encrypted => {
      this.proposalService.createProposal({
        ...encrypted,
        mentionedUserIds: this.mentionedUserIds
      }).subscribe({
        next: result => this.handleCreateResult(result),
        error: err => this.handleCreateError(err)
      });
    }).catch(() => {
      this.toastService.error('Failed to encrypt proposal content.');
      this.isSubmitting = false;
      this.updateCreateButton();
    });
  }

  private handleCreateResult(result: { success: boolean; message?: string }) {
    if (result.success) {
      this.toastService.success(result.message || 'Proposal created');
      const listPath = this.isFleetScope
        ? ['/app/fleet/proposals/list/pending']
        : ['/app/crew/proposals/list/pending'];
      void this.router.navigate(listPath);
      return;
    }
    this.toastService.error(result.message || 'Failed to create proposal');
    this.isSubmitting = false;
    this.updateCreateButton();
  }

  private handleCreateError(err: { error?: { message?: string } }) {
    this.toastService.error(err?.error?.message || 'Failed to create proposal');
    this.isSubmitting = false;
    this.updateCreateButton();
  }

  private updateCreateButton() {
    this.createButton = {
      label: 'Create',
      type: 'primary',
      disabled: this.isSubmitting || this.form.invalid || !this.canCreateProposals,
      onClick: () => this.onSubmit()
    };
  }
}
