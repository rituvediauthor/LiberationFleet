import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LibraryUnitListItem } from '../../models/library.model';

@Component({
  selector: 'app-library-item-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './library-item-card.component.html',
  styleUrl: './library-item-card.component.css'
})
export class LibraryItemCardComponent {
  @Input({ required: true }) item!: LibraryUnitListItem;
  @Input() holderLabel = 'Holder';

  get showAvailableNow(): boolean {
    return this.item.offeringKind === 'Durable'
      && this.item.availableNow === true
      && !this.item.isOutOfStock;
  }

  get showNextAvailable(): boolean {
    return this.item.offeringKind === 'Durable'
      && this.item.availableNow === false
      && !!this.item.nextAvailableDate;
  }

  get showUnitCount(): boolean {
    return this.item.offeringKind === 'Durable'
      && (this.item.offeringUnitCount ?? 0) > 1
      && this.item.availableNow !== true;
  }
}
