import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BRAND_LOGO_ASSETS, BrandLogoVariant } from '../../constants/brand-assets';

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
  /**
   * Inactive / muted treatment (e.g. bottom nav when the tab is not selected).
   * Applies a CSS greyscale filter to the brand asset. Custom images are left in color
   * (nav uses opacity for those).
   */
  @Input() monochrome = false;
  /** Optional decrypted custom image (crew/fleet image). Falls back to brand assets when empty. */
  @Input() customSrc: string | null = null;

  get resolvedSrc(): string {
    if (this.customSrc) {
      return this.customSrc;
    }
    return BRAND_LOGO_ASSETS[this.variant];
  }

  /** Theme-tinted mask for built-in crew/fleet marks (not custom uploads or LF wordmark). */
  get useThemedMask(): boolean {
    return !this.customSrc && (this.variant === 'crew' || this.variant === 'fleet');
  }

  get maskImage(): string | null {
    if (!this.useThemedMask) {
      return null;
    }
    return `url("${this.resolvedSrc}")`;
  }

  get useGreyscale(): boolean {
    return this.monochrome && !this.customSrc;
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
    if (this.variant === 'fleet') {
      return 'Fleet logo';
    }
    return 'LiberationFleet logo';
  }
}
