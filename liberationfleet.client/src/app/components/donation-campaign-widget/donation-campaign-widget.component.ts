import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { DonationService } from '../../services/donation.service';

@Component({
  selector: 'app-donation-campaign-widget',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './donation-campaign-widget.component.html',
  styleUrl: './donation-campaign-widget.component.css'
})
export class DonationCampaignWidgetComponent implements OnChanges {
  @Input() variant: 'crew' | 'fleet' = 'crew';
  @Input() enabled = true;
  @Output() visibilityChange = new EventEmitter<boolean>();

  visible = false;
  message = '';
  private acknowledged = false;

  private donationService = inject(DonationService);
  private router = inject(Router);

  ngOnChanges(changes: SimpleChanges) {
    if (changes['enabled'] || changes['variant']) {
      if (this.enabled) {
        this.loadPrompt();
      } else {
        this.setVisible(false);
      }
    }
  }

  goDonate(event?: Event) {
    event?.stopPropagation();
    this.acknowledgeOnce();
    void this.router.navigate(['/app/donate']);
  }

  dismiss(event: Event) {
    event.stopPropagation();
    this.acknowledgeOnce();
    this.setVisible(false);
  }

  private loadPrompt() {
    this.donationService.getCampaignPrompt(this.variant).subscribe({
      next: prompt => {
        this.message = prompt.message;
        this.setVisible(!!prompt.show);
        if (prompt.show) {
          this.acknowledgeOnce();
        }
      },
      error: () => this.setVisible(false)
    });
  }

  private acknowledgeOnce() {
    if (this.acknowledged) {
      return;
    }
    this.acknowledged = true;
    this.donationService.acknowledgeCampaignPrompt().subscribe({ error: () => undefined });
  }

  private setVisible(show: boolean) {
    this.visible = show;
    this.visibilityChange.emit(show);
  }
}
