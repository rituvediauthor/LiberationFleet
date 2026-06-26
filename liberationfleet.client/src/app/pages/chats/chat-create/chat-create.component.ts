import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ChatService } from '../../../services/chat.service';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ChatRoomType } from '../../../models/chat.model';

@Component({
  selector: 'app-chat-create',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageLayoutComponent],
  templateUrl: './chat-create.component.html',
  styleUrl: './chat-create.component.css'
})
export class ChatCreateComponent implements OnInit {
  form: FormGroup;
  isSubmitting = false;
  crewId = 0;
  requireApprovalForEdits = true;
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;

  readonly roomTypes: { value: ChatRoomType; label: string }[] = [
    { value: 'Text', label: 'Text chat' },
    { value: 'Voice', label: 'Voice chat' }
  ];

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private chatService = inject(ChatService);
  private chatCrypto = inject(ChatCryptoService);
  private crewService = inject(CrewService);
  private encryptionContent = inject(EncryptionContentService);
  private toastService = inject(ToastService);

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(120)]],
      purpose: ['', [Validators.required, Validators.maxLength(2000)]],
      roomType: ['Text', Validators.required]
    });
  }

  ngOnInit() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/chats'])
    };
    this.updateCreateButton();
    this.form.statusChanges.subscribe(() => this.updateCreateButton());
    this.form.valueChanges.subscribe(() => this.updateCreateButton());

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
  }

  get createButtonLabel(): string {
    return this.requireApprovalForEdits ? 'Submit proposal' : 'Create Chat';
  }

  async onSubmit() {
    if (this.form.invalid || this.isSubmitting || this.crewId <= 0) {
      return;
    }

    this.isSubmitting = true;
    this.updateCreateButton();

    try {
      await this.encryptionContent.whenReady();
      const value = this.form.getRawValue();
      const name = String(value.name).trim();
      const purpose = String(value.purpose).trim();
      const encrypted = await this.chatCrypto.encryptRoomName(this.crewId, name);

      this.chatService.createRoom({
        nonce: encrypted.nonce,
        ciphertext: encrypted.ciphertext,
        roomType: value.roomType as ChatRoomType,
        purpose,
        plaintextName: name
      }).subscribe({
        next: response => {
          this.isSubmitting = false;
          this.updateCreateButton();
          if (response.success && response.proposalsSubmitted) {
            this.toastService.success(response.message || 'Proposal submitted for crew approval');
            this.router.navigate(['/app/crew/proposals/list/pending']);
            return;
          }
          if (!response.success) {
            this.toastService.error(response.message || 'Failed to create chat room');
            return;
          }
          this.toastService.success('Chat room created');
          this.router.navigate(['/app/crew/chats']);
        },
        error: () => {
          this.isSubmitting = false;
          this.updateCreateButton();
          this.toastService.error('Failed to create chat room');
        }
      });
    } catch {
      this.isSubmitting = false;
      this.updateCreateButton();
      this.toastService.error('Failed to encrypt chat room name');
    }
  }

  private updateCreateButton() {
    this.createButton = {
      label: this.createButtonLabel,
      type: 'primary',
      disabled: this.form.invalid || this.isSubmitting,
      onClick: () => void this.onSubmit()
    };
  }
}
