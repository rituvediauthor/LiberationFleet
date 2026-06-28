import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CUSTOM_PLATFORM_OPTION_ID, PaymentPlatformAccount } from '../../models/profile.model';
import { PaymentPlatformOption } from '../../models/gift.model';
import { ProfileService } from '../../services/profile.service';

@Component({
  selector: 'app-payment-platform-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './payment-platform-editor.component.html',
  styleUrl: './payment-platform-editor.component.css'
})
export class PaymentPlatformEditorComponent {
  @Input() platforms: PaymentPlatformAccount[] = [];
  @Input() platformOptions: PaymentPlatformOption[] = [];
  @Input() showPreferred = false;
  @Output() changed = new EventEmitter<void>();
  @Output() add = new EventEmitter<void>();
  @Output() remove = new EventEmitter<number>();
  @Output() preferredChange = new EventEmitter<number>();

  readonly customPlatformOptionId = CUSTOM_PLATFORM_OPTION_ID;

  private profileService = inject(ProfileService);

  isCustomPlatform(account: PaymentPlatformAccount): boolean {
    return this.profileService.isCustomPlatform(account);
  }

  fieldId(account: PaymentPlatformAccount, suffix: string): string {
    return `payment-platform-${account.id}-${suffix}`;
  }

  onFieldChange() {
    this.changed.emit();
  }

  onAdd() {
    this.add.emit();
  }

  onRemove(accountId: number) {
    this.remove.emit(accountId);
  }

  onPreferredChange(accountId: number) {
    this.preferredChange.emit(accountId);
  }
}
