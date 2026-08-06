import {
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnDestroy,
  Output,
  ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { AccessibleDialogDirective } from '../../directives/accessible-dialog.directive';
import { enterMediaDetailZoom, exitMediaDetailZoom } from '../../utils/media-viewport-zoom';

@Component({
  selector: 'app-library-image-carousel',
  standalone: true,
  imports: [CommonModule, AccessibleDialogDirective],
  templateUrl: './library-image-carousel.component.html',
  styleUrl: './library-image-carousel.component.css',
  host: {
    '[class.variant-card]': 'variant === "card"',
    '[class.variant-inline]': 'variant === "inline"'
  }
})
export class LibraryImageCarouselComponent implements OnDestroy {
  @Input() images: string[] = [];
  @Input() title = '';
  @Input() variant: 'hero' | 'inline' | 'card' = 'hero';
  @Output() imageClick = new EventEmitter<number>();

  @ViewChild('lightbox') set lightboxRef(ref: ElementRef<HTMLElement> | undefined) {
    const el = ref?.nativeElement;
    if (!el) {
      return;
    }
    // Mount on body so position:fixed is not trapped by .page-content overflow on mobile.
    if (el.parentElement !== document.body) {
      document.body.appendChild(el);
    }
  }

  activeIndex = 0;
  selectedIndex: number | null = null;
  closeFullBound = () => this.closeFull();

  openFull(index: number) {
    this.selectedIndex = index;
    enterMediaDetailZoom();
    this.imageClick.emit(index);
  }

  closeFull() {
    if (this.selectedIndex !== null) {
      this.activeIndex = this.selectedIndex;
      exitMediaDetailZoom();
    }
    this.selectedIndex = null;
  }

  ngOnDestroy() {
    if (this.selectedIndex !== null) {
      exitMediaDetailZoom();
      this.selectedIndex = null;
    }
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
