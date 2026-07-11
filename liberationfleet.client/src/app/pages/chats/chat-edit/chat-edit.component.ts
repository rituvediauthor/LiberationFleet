import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ChatService } from '../../../services/chat.service';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ChatRoomType } from '../../../models/chat.model';
import { isSaveActionDisabled } from '../../../utils/save-button.util';

@Component({
  selector: 'app-chat-edit',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './chat-edit.component.html',
  styleUrl: './chat-edit.component.css'
})
export class ChatEditComponent implements OnInit {
  form!: FormGroup;
  isSubmitting = false;
  isDeleting = false;
  loading = true;
  loadError = '';
  crewId = 0;
  roomId = 0;
  requireApprovalForEdits = true;
  backButton!: ActionBarButton;
  saveButton!: ActionBarButton;
  private initialFormValues: { name: string; purpose: string; roomType: ChatRoomType } | null = null;

  readonly roomTypes: { value: ChatRoomType; label: string }[] = [
    { value: 'Text', label: 'Text chat' },
    { value: 'Voice', label: 'Voice chat' }
  ];

  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  private navigation = inject(NavigationService);
  private chatService = inject(ChatService);
  private chatCrypto = inject(ChatCryptoService);
  private crewService = inject(CrewService);
  private encryptionContent = inject(EncryptionContentService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.roomId = Number(this.route.snapshot.paramMap.get('id'));

    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(120)]],
      purpose: ['', [Validators.required, Validators.maxLength(2000)]],
      roomType: ['Text', Validators.required]
    });

    this.backButton = this.navigation.createBackButton(['/app/crew/chats']);

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
        this.loadRoom();
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

  async onSubmit() {
    if (this.form.invalid || this.isSubmitting || this.crewId <= 0) {
      return;
    }

    this.isSubmitting = true;
    this.updateSaveButton();

    try {
      const value = this.form.getRawValue();
      const name = String(value.name).trim();
      const purpose = String(value.purpose).trim();
      const encrypted = await this.chatCrypto.encryptRoomName(this.crewId, name);
      const oldValues = this.initialFormValues ?? { name: '', purpose: '', roomType: 'Text' as ChatRoomType };

      this.chatService.updateRoom(this.roomId, {
        nonce: encrypted.nonce,
        ciphertext: encrypted.ciphertext,
        roomType: value.roomType as ChatRoomType,
        purpose,
        plaintextName: name,
        plaintextOldName: oldValues.name,
        plaintextOldPurpose: oldValues.purpose
      }).subscribe({
        next: response => {
          if (response.success && response.proposalsSubmitted) {
            this.toastService.success(response.message || 'Proposal submitted for crew approval');
            this.router.navigate(['/app/crew/proposals/list/pending']);
            return;
          }
          if (response.success) {
            this.toastService.success('Chat room saved');
            this.router.navigate(['/app/crew/chats']);
            return;
          }
          this.toastService.error(response.message || 'Failed to save chat room');
          this.isSubmitting = false;
          this.updateSaveButton();
        },
        error: () => {
          this.toastService.error('Failed to save chat room');
          this.isSubmitting = false;
          this.updateSaveButton();
        }
      });
    } catch {
      this.toastService.error('Failed to encrypt chat room name');
      this.isSubmitting = false;
      this.updateSaveButton();
    }
  }

  deleteRoom() {
    if (this.isDeleting || this.isSubmitting) {
      return;
    }

    const values = this.initialFormValues ?? this.form.getRawValue();
    this.isDeleting = true;
    this.chatService.deleteRoom(this.roomId, {
      plaintextName: String(values.name).trim(),
      plaintextPurpose: String(values.purpose).trim()
    }).subscribe({
      next: response => {
        this.isDeleting = false;
        if (response.success && response.proposalsSubmitted) {
          this.toastService.success(response.message || 'Proposal submitted for crew approval');
          this.router.navigate(['/app/crew/proposals/list/pending']);
          return;
        }
        if (response.success) {
          this.toastService.success('Chat room deleted');
          this.router.navigate(['/app/crew/chats']);
          return;
        }
        this.toastService.error(response.message || 'Failed to delete chat room');
      },
      error: () => {
        this.isDeleting = false;
        this.toastService.error('Failed to delete chat room');
      }
    });
  }

  private loadRoom() {
    this.loading = true;
    this.loadError = '';
    this.chatService.getRoom(this.roomId).subscribe({
      next: async response => {
        try {
          if (!response.success || !response.room) {
            this.loadError = response.message || 'Failed to load chat room';
            return;
          }
          const decrypted = this.crewId > 0
            ? await this.chatCrypto.decryptRoom(response.room, this.crewId)
            : response.room;
          this.form.patchValue({
            name: decrypted.name ?? '',
            purpose: decrypted.purpose ?? '',
            roomType: decrypted.roomType
          });
          this.initialFormValues = this.form.getRawValue();
        } finally {
          this.loading = false;
          this.updateSaveButton();
        }
      },
      error: () => {
        this.loading = false;
        this.loadError = 'Failed to load chat room';
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
