import { Component, OnDestroy, OnInit, inject } from '@angular/core';
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

    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
      }
    });
  }

  async onSubmit() {
    if (this.form.invalid || this.isSubmitting || this.crewId <= 0) {
      return;
    }

    this.isSubmitting = true;
    this.updateCreateButton();

    try {
      await this.encryptionContent.whenReady();
      const value = this.form.value;
      const encrypted = await this.chatCrypto.encryptRoomName(this.crewId, value.name.trim());

      this.chatService.createRoom({
        nonce: encrypted.nonce,
        ciphertext: encrypted.ciphertext,
        roomType: value.roomType as ChatRoomType
      }).subscribe({
        next: response => {
          this.isSubmitting = false;
          this.updateCreateButton();
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
      label: 'Create Chat',
      type: 'primary',
      disabled: this.form.invalid || this.isSubmitting,
      onClick: () => void this.onSubmit()
    };
  }
}
