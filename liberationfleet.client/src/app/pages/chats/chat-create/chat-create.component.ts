import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NavigationService } from '../../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ChatService } from '../../../services/chat.service';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { FleetService } from '../../../services/fleet.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ChatRoomType } from '../../../models/chat.model';
import { isControlInvalidForA11y } from '../../../utils/a11y-form.util';

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
  fleetId = 0;
  isFleetScope = false;
  requireApprovalForEdits = true;
  backButton!: ActionBarButton;
  createButton!: ActionBarButton;

  readonly roomTypes: { value: ChatRoomType; label: string }[] = [
    { value: 'Text', label: 'Text chat' },
    { value: 'Voice', label: 'Voice chat' }
  ];

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  private navigation = inject(NavigationService);
  private chatService = inject(ChatService);
  private chatCrypto = inject(ChatCryptoService);
  private crewService = inject(CrewService);
  private fleetService = inject(FleetService);
  private encryptionContent = inject(EncryptionContentService);
  private toastService = inject(ToastService);

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(120)]],
      purpose: ['', [Validators.required, Validators.maxLength(2000)]],
      roomType: ['Text', Validators.required],
      isAdultContent: [false]
    });
  }

  ngOnInit() {
    this.isFleetScope = this.route.snapshot.data['scope'] === 'fleet';
    this.backButton = this.navigation.createBackButton(
      this.isFleetScope ? ['/app/fleet/chats'] : ['/app/crew/chats']
    );
    this.updateCreateButton();
    this.form.statusChanges.subscribe(() => this.updateCreateButton());
    this.form.valueChanges.subscribe(() => this.updateCreateButton());

    if (this.isFleetScope) {
      this.form.get('roomType')?.setValue('Text');
      this.form.get('roomType')?.disable({ emitEvent: false });
      this.fleetService.getCurrent().subscribe({
        next: result => {
          if (result.success && result.fleet) {
            this.fleetId = result.fleet.id;
            this.requireApprovalForEdits = result.fleet.requireApprovalForEdits ?? true;
            this.updateCreateButton();
          }
        }
      });
      this.fleetService.getStatus().subscribe({
        next: status => {
          this.fleetId = status.fleetId ?? this.fleetId;
        }
      });
      return;
    }

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

  get availableRoomTypes(): { value: ChatRoomType; label: string }[] {
    return this.isFleetScope
      ? this.roomTypes.filter(t => t.value === 'Text')
      : this.roomTypes;
  }

  isInvalid(controlName: string): boolean {
    return isControlInvalidForA11y(this.form.get(controlName));
  }

  async onSubmit() {
    if (this.form.invalid || this.isSubmitting) {
      return;
    }

    if (this.isFleetScope) {
      if (this.fleetId <= 0) {
        return;
      }
    } else if (this.crewId <= 0) {
      return;
    }

    this.isSubmitting = true;
    this.updateCreateButton();

    try {
      await this.encryptionContent.whenReady();
      const value = this.form.getRawValue();
      const name = String(value.name).trim();
      const purpose = String(value.purpose).trim();
      const encrypted = this.isFleetScope
        ? await this.chatCrypto.encryptRoomName({ fleetId: this.fleetId }, name)
        : await this.chatCrypto.encryptRoomName({ crewId: this.crewId }, name);

      this.chatService.createRoom({
        nonce: encrypted.nonce,
        ciphertext: encrypted.ciphertext,
        roomType: (this.isFleetScope ? 'Text' : value.roomType) as ChatRoomType,
        purpose,
        plaintextName: name,
        isAdultContent: !!value.isAdultContent,
        scope: this.isFleetScope ? 'fleet' : 'crew'
      }).subscribe({
        next: response => {
          this.isSubmitting = false;
          this.updateCreateButton();
          if (response.success && response.proposalsSubmitted) {
            this.toastService.success(
              response.message
                || (this.isFleetScope
                  ? 'Proposal submitted for fleet approval'
                  : 'Proposal submitted for crew approval')
            );
            this.router.navigate(
              this.isFleetScope
                ? ['/app/fleet/proposals/list/pending']
                : ['/app/crew/proposals/list/pending']
            );
            return;
          }
          if (!response.success) {
            this.toastService.error(response.message || 'Failed to create chat room');
            return;
          }
          this.toastService.success('Chat room created');
          this.router.navigate(this.isFleetScope ? ['/app/fleet/chats'] : ['/app/crew/chats']);
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
      disabled: this.form.invalid || this.isSubmitting || (this.isFleetScope ? this.fleetId <= 0 : this.crewId <= 0),
      onClick: () => void this.onSubmit()
    };
  }
}
