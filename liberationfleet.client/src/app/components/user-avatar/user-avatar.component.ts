import { Component, Input, OnChanges, OnDestroy, OnInit, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { EncryptedImageCacheService } from '../../services/encrypted-image-cache.service';
import { CryptoSessionService } from '../../services/crypto/crypto-session.service';
import { EncryptedContentType } from '../../models/crypto.model';

@Component({
  selector: 'app-user-avatar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './user-avatar.component.html',
  styleUrl: './user-avatar.component.css'
})
export class UserAvatarComponent implements OnInit, OnChanges, OnDestroy {
  /** When true, hide the picture and show the fallback glyph (anonymous content). */
  @Input() anonymous = false;
  @Input() resourceId: string | null | undefined;
  @Input() crewId: number | null | undefined;
  @Input() fleetId: number | null | undefined;
  @Input() contentType: Extract<EncryptedContentType, 'ProfileAvatar' | 'ImageAsset'> = 'ProfileAvatar';
  @Input() size: 'xs' | 'sm' | 'md' | 'lg' = 'sm';
  @Input() alt = '';
  /** Optional initial letter when no image (e.g. username first char). */
  @Input() fallbackInitial = '';

  src: string | null = null;
  private readonly images = inject(EncryptedImageCacheService);
  private readonly cryptoSession = inject(CryptoSessionService);
  private unlockSub?: Subscription;

  ngOnInit(): void {
    this.unlockSub = this.cryptoSession.unlocked$.subscribe(unlocked => {
      if (unlocked) {
        void this.load();
      }
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (
      changes['anonymous'] ||
      changes['resourceId'] ||
      changes['crewId'] ||
      changes['fleetId'] ||
      changes['contentType']
    ) {
      void this.load();
    }
  }

  ngOnDestroy(): void {
    this.unlockSub?.unsubscribe();
  }

  private async load(): Promise<void> {
    this.src = null;
    if (this.anonymous || !this.resourceId?.trim()) {
      return;
    }

    const scope =
      this.crewId != null && this.crewId > 0
        ? { crewId: this.crewId }
        : this.fleetId != null && this.fleetId > 0
          ? { fleetId: this.fleetId }
          : null;
    if (!scope) {
      return;
    }

    const requested = this.resourceId.trim();
    const dataUrl = await this.images.getDataUrl(scope, requested, this.contentType);
    if (this.resourceId?.trim() === requested) {
      this.src = dataUrl;
    }
  }

  get initial(): string {
    const raw = this.fallbackInitial?.trim();
    return raw ? raw.charAt(0).toUpperCase() : '';
  }
}
