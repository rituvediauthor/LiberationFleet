import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-library-image-carousel',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './library-image-carousel.component.html',
  styleUrl: './library-image-carousel.component.css'
})
export class LibraryImageCarouselComponent {
  @Input() images: string[] = [];
  @Input() title = '';
  @Output() imageClick = new EventEmitter<number>();

  selectedIndex: number | null = null;

  openFull(index: number) {
    this.selectedIndex = index;
    this.imageClick.emit(index);
  }

  closeFull() {
    this.selectedIndex = null;
  }

  showPrevious() {
    if (this.selectedIndex === null || this.images.length === 0) {
      return;
    }
    this.selectedIndex = (this.selectedIndex - 1 + this.images.length) % this.images.length;
  }

  showNext() {
    if (this.selectedIndex === null || this.images.length === 0) {
      return;
    }
    this.selectedIndex = (this.selectedIndex + 1) % this.images.length;
  }
}
