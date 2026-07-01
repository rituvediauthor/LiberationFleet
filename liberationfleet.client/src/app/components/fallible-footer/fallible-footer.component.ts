import { Component, inject } from '@angular/core';
import { FallibleService } from '../../services/fallible.service';

const FALLIBLE_DOC_URL =
  'https://docs.google.com/document/d/119Om2v-ytW_VNzFSc_aE8kj6AphuWt0AZ6IUtlDdq6o/edit?usp=sharing';

@Component({
  selector: 'app-fallible-footer',
  standalone: true,
  templateUrl: './fallible-footer.component.html',
  styleUrl: './fallible-footer.component.css'
})
export class FallibleFooterComponent {
  private fallibleService = inject(FallibleService);

  onImageClick(event: Event) {
    event.preventDefault();
    this.fallibleService.recordClick().subscribe({ error: () => {} });
    window.open(FALLIBLE_DOC_URL, '_blank', 'noopener,noreferrer');
  }
}
