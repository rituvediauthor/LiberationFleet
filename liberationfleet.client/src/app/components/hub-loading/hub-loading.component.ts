import { Component, Input } from '@angular/core';
import { BrandLogoComponent } from '../brand-logo/brand-logo.component';
import { BrandLogoVariant } from '../../constants/brand-assets';

@Component({
  selector: 'app-hub-loading',
  standalone: true,
  imports: [BrandLogoComponent],
  templateUrl: './hub-loading.component.html',
  styleUrl: './hub-loading.component.css'
})
export class HubLoadingComponent {
  @Input() variant: BrandLogoVariant = 'lf';
  @Input() label = 'Loading...';
}
