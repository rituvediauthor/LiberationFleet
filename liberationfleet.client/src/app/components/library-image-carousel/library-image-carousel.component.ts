import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AccessibleDialogDirective } from '../../directives/accessible-dialog.directive';

@Component({
  selector: 'app-library-image-carousel',
  standalone: true,
  imports: [CommonModule, AccessibleDialogDirective],
  templateUrl: './library-image-carousel.component.html',
  styleUrl: './library-image-carousel.component.css'
})
export class LibraryImageCarouselComponent {
  @Input() images: string[] = [];
  @Input() title = '';
  @Input() variant: 'hero' | 'inline' = 'hero';
  @Output() imageClick = new EventEmitter<number>();

  activeIndex = 0;
  selectedIndex: number | null = null;
  closeFullBound = () => this.closeFull();

  openFull(index: number) {
    this.selectedIndex = index;
    this.imageClick.emit(index);
  }

  closeFull() {
    if (this.selectedIndex !== null) {
      this.activeIndex = this.selectedIndex;
    }
    this.selectedIndex = null;
  }

  showPrevious(event?: Event) {
    event?.stopPropagation();
    event?.preventDefault();
    if (this.images.length === 0) {
      return;
    }
    this.activeIndex = (this.activeIndex - 1 + this.images.length) % this.images.length;
  }

  showNext(event?: Event) {
    event?.stopPropagation();
    event?.preventDefault();
    if (this.images.length === 0) {
      return;
    }
    this.activeIndex = (this.activeIndex + 1) % this.images.length;
  }

  showPreviousInLightbox(event?: Event) {
    event?.stopPropagation();
    event?.preventDefault();
    if (this.selectedIndex === null || this.images.length === 0) {
      return;
    }
    this.selectedIndex = (this.selectedIndex - 1 + this.images.length) % this.images.length;
  }

  showNextInLightbox(event?: Event) {
    event?.stopPropagation();
    event?.preventDefault();
    if (this.selectedIndex === null || this.images.length === 0) {
      return;
    }
    this.selectedIndex = (this.selectedIndex + 1) % this.images.length;
  }
}
