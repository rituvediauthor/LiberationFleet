import { AsyncPipe, NgIf } from '@angular/common';
import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ClientConfigService } from '../../services/client-config.service';
import { FallibleService } from '../../services/fallible.service';
import { navigateToDonate } from '../../utils/donation-nav.util';

const FALLIBLE_DOC_URL =
  'https://docs.google.com/document/d/119Om2v-ytW_VNzFSc_aE8kj6AphuWt0AZ6IUtlDdq6o/edit?usp=sharing';

@Component({
  selector: 'app-fallible-footer',
  standalone: true,
  imports: [AsyncPipe, NgIf],
  templateUrl: './fallible-footer.component.html',
  styleUrl: './fallible-footer.component.css'
})
export class FallibleFooterComponent {
  private fallibleService = inject(FallibleService);
  private clientConfig = inject(ClientConfigService);
  private router = inject(Router);

  readonly showAttribution$ = this.clientConfig.showFallibleAttribution$;

  onImageClick() {
    this.fallibleService.recordClick().subscribe({ error: () => {} });
    window.open(FALLIBLE_DOC_URL, '_blank', 'noopener,noreferrer');
  }

  onDonateClick(event: Event) {
    event.preventDefault();
    navigateToDonate(this.router);
  }
}
