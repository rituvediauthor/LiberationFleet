import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { ChatService } from '../../../services/chat.service';
import { ChatCryptoService } from '../../../services/crypto/chat-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { GiftService } from '../../../services/gift.service';
import { ProfileService } from '../../../services/profile.service';
import { CrewMember } from '../../../models/gift.model';
import { ToastService } from '../../../components/toast/toast.component';

@Component({
  selector: 'app-chat-voice',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './chat-voice.component.html',
  styleUrl: './chat-voice.component.css'
})
export class ChatVoiceComponent implements OnInit {
  roomId = 0;
  roomName = 'Voice chat';
  members: CrewMember[] = [];
  loading = true;
  backButton!: ActionBarButton;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private chatService = inject(ChatService);
  private chatCrypto = inject(ChatCryptoService);
  private crewService = inject(CrewService);
  private encryptionContent = inject(EncryptionContentService);
  private giftService = inject(GiftService);
  private profileService = inject(ProfileService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.roomId = Number(this.route.snapshot.paramMap.get('id'));
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/chats'])
    };

    this.crewService.getMembership().subscribe({
      next: async membership => {
        const crewId = membership.crewId ?? 0;
        await this.encryptionContent.whenReady();
        this.chatService.getRooms().subscribe({
          next: async response => {
            const room = response.items?.find(item => item.id === this.roomId);
            if (!room) {
              return;
            }
            const decrypted = crewId > 0
              ? await this.chatCrypto.decryptRoom(room, crewId)
              : room;
            this.roomName = decrypted.name || 'Voice chat';
          }
        });
      }
    });

    this.profileService.getProfile().subscribe({
      next: profile => {
        this.giftService.getCrewMembers(profile.id).subscribe({
          next: members => {
            this.members = members;
            this.loading = false;
          },
          error: () => {
            this.loading = false;
            this.toastService.error('Failed to load crew members');
          }
        });
      },
      error: () => {
        this.loading = false;
        this.toastService.error('Failed to load profile');
      }
    });
  }
}
