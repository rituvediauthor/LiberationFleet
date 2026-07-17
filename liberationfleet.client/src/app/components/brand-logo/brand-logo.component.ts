import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BRAND_LOGO_ASSETS, BrandLogoVariant } from '../../constants/brand-assets';
import { ThemeService } from '../../services/theme.service';
import { AppThemeId } from '../../models/theme.model';

@Component({
  selector: 'app-brand-logo',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './brand-logo.component.html',
  styleUrl: './brand-logo.component.css'
})
export class BrandLogoComponent {
  @Input({ required: true }) variant!: BrandLogoVariant;
  @Input() size: 'sm' | 'md' | 'lg' = 'md';
  @Input() alt = '';
  /** When true, image is treated as decorative (empty alt) even if beside labeled text/button. */
  @Input() decorative = false;
  @Input() monochrome = false;
  /** Optional decrypted custom image (crew/fleet image). Falls back to brand assets when empty. */
  @Input() customSrc: string | null = null;

  readonly themeService = inject(ThemeService);

  get hexSrc(): string {
    return BRAND_LOGO_ASSETS[this.variant].hex;
  }

  get greySrc(): string {
    return BRAND_LOGO_ASSETS[this.variant].grey;
  }

  resolveSrc(theme: AppThemeId | null | undefined): string {
    if (this.customSrc) {
      return this.customSrc;
    }
    if (this.monochrome) {
      return this.greySrc;
    }
    return theme === 'light' ? this.greySrc : this.hexSrc;
  }

  get resolvedAlt(): string {
    if (this.decorative) {
      return '';
    }
    if (this.alt) {
      return this.alt;
    }

    if (this.variant === 'crew') {
      return 'Crew logo';
    }
    return 'LiberationFleet logo';
  }
}
