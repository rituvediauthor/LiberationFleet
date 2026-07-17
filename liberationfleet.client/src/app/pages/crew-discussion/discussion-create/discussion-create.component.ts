import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ProposalAttachmentPickerComponent } from '../../../components/proposal-attachment-picker/proposal-attachment-picker.component';
import { CrewDiscussionService } from '../../../services/crew-discussion.service';
import { ProposalCryptoService } from '../../../services/crypto/proposal-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ProfileService } from '../../../services/profile.service';
import { ToastService } from '../../../components/toast/toast.component';
import { NavigationService } from '../../../services/navigation.service';
import { DiscussionConfig, DiscussionKind, getDiscussionConfig } from '../../../config/discussion.config';
import { PendingAttachment } from '../../../models/crew-discussion.model';
import { MentionAutocompleteDirective } from '../../../directives/mention-autocomplete.directive';
import { isControlInvalidForA11y } from '../../../utils/a11y-form.util';
import { truncateNotificationPreview } from '../../../utils/notification-preview.util';

@Component({
  selector: 'app-discussion-create',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent, ProposalAttachmentPickerComponent, MentionAutocompleteDirective],
  templateUrl: './discussion-create.component.html',
  styleUrl: './discussion-create.component.css'
})
export class DiscussionCreateComponent implements OnInit {
  config!: DiscussionConfig;
  form!: FormGroup;
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  attachments: PendingAttachment[] = [];
  isSubmitting = false;
  crewId = 0;
  canAttachFiles = false;
  mentionedUserIds: number[] = [];
  authorDisplayName = '';

  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private discussionService = inject(CrewDiscussionService);
  private discussionCrypto = inject(ProposalCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);

  ngOnInit() {
    const kind = this.route.snapshot.data['discussionKind'] as DiscussionKind;
    this.config = getDiscussionConfig(kind);

    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', [Validators.required, Validators.maxLength(10000)]],
      isAdultContent: [false]
    });

    this.backButton = this.navigation.createBackButton([this.config.listRoute]);

    this.updateCreateButton();

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
        this.canAttachFiles = membership.canAttachFilesToCrewContent ?? false;
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

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form?.get(controlName));
  }

  onSubmit() {
    if (this.form.invalid || this.isSubmitting || this.crewId <= 0) {
      return;
    }

    this.isSubmitting = true;
    this.updateCreateButton();

    const { title, description, isAdultContent } = this.form.getRawValue();
    this.discussionCrypto.encryptProposalPayload(
      this.crewId,
      {
        title: title.trim(),
        description: description.trim(),
        authorDisplayName: this.authorDisplayName
      },
      this.attachments
    ).then(encrypted => {
      this.discussionService.createPost(this.config, {
        ...encrypted,
        isAdultContent: !!isAdultContent,
        mentionedUserIds: this.mentionedUserIds,
        notificationPreview: truncateNotificationPreview(description.trim())
      }).subscribe({
        next: result => {
          if (result.success) {
            this.toastService.success(result.message || `${this.config.label} post created`);
            this.router.navigate([this.config.listRoute]);
            return;
          }
          this.toastService.error(result.message || `Failed to create ${this.config.postLabel}`);
          this.isSubmitting = false;
          this.updateCreateButton();
        },
        error: err => {
          this.toastService.error(err?.error?.message || `Failed to create ${this.config.postLabel}`);
          this.isSubmitting = false;
          this.updateCreateButton();
        }
      });
    }).catch(() => {
      this.toastService.error('Failed to encrypt post content.');
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
