import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../../components/page-layout/page-layout.component';
import { ProposalAttachmentPickerComponent } from '../../../../components/proposal-attachment-picker/proposal-attachment-picker.component';
import { FleetService } from '../../../../services/fleet.service';
import { ProposalCryptoService } from '../../../../services/crypto/proposal-crypto.service';
import { CrewService } from '../../../../services/crew.service';
import { ProfileService } from '../../../../services/profile.service';
import { ToastService } from '../../../../components/toast/toast.component';
import { NavigationService } from '../../../../services/navigation.service';
import { PendingAttachment } from '../../../../models/fleet-forum.model';
import { MentionAutocompleteDirective } from '../../../../directives/mention-autocomplete.directive';
import { isControlInvalidForA11y } from '../../../../utils/a11y-form.util';
import { truncateNotificationPreview } from '../../../../utils/notification-preview.util';

@Component({
  selector: 'app-fleet-forum-create',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageLayoutComponent,
    ProposalAttachmentPickerComponent,
    MentionAutocompleteDirective
  ],
  templateUrl: './fleet-forum-create.component.html',
  styleUrl: './fleet-forum-create.component.css'
})
export class FleetForumCreateComponent implements OnInit {
  form!: FormGroup;
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;
  attachments: PendingAttachment[] = [];
  isSubmitting = false;
  fleetId = 0;
  canAttachFiles = false;
  mentionedUserIds: number[] = [];
  authorDisplayName = '';

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private navigation = inject(NavigationService);
  private fleetService = inject(FleetService);
  private forumCrypto = inject(ProposalCryptoService);
  private crewService = inject(CrewService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', [Validators.required, Validators.maxLength(10000)]],
      isAdultContent: [false]
    });

    this.backButton = this.navigation.createBackButton(['/app/fleet/forums']);
    this.updateCreateButton();

    this.fleetService.getStatus().subscribe({
      next: status => {
        this.fleetId = status.fleetId ?? 0;
      }
    });

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.canAttachFiles = membership.canAttachFilesToFleetContent ?? false;
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
    if (this.form.invalid || this.isSubmitting || this.fleetId <= 0) {
      return;
    }

    this.isSubmitting = true;
    this.updateCreateButton();

    const { title, description, isAdultContent } = this.form.getRawValue();
    this.forumCrypto.encryptProposalPayload(
      { fleetId: this.fleetId },
      {
        title: title.trim(),
        description: description.trim(),
        authorDisplayName: this.authorDisplayName
      },
      this.attachments
    ).then(encrypted => {
      this.fleetService.createForum({
        ...encrypted,
        isAdultContent: !!isAdultContent,
        mentionedUserIds: this.mentionedUserIds,
        notificationPreview: truncateNotificationPreview(description.trim())
      }).subscribe({
        next: result => {
          if (result.success) {
            this.toastService.success(result.message || 'Post created');
            this.router.navigate(['/app/fleet/forums']);
            return;
          }
          this.toastService.error(result.message || 'Failed to create post');
          this.isSubmitting = false;
          this.updateCreateButton();
        },
        error: err => {
          this.toastService.error(err?.error?.message || 'Failed to create post');
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
