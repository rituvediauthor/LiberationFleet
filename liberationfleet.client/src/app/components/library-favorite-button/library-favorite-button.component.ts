import { Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { LibraryService } from '../../services/library.service';
import { ToastService } from '../toast/toast.component';

@Component({
  selector: 'app-library-favorite-button',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './library-favorite-button.component.html',
  styleUrl: './library-favorite-button.component.css'
})
export class LibraryFavoriteButtonComponent implements OnInit, OnChanges, OnDestroy {
  @Input({ required: true }) unitId = 0;
  @Input() placement: 'overlay' | 'inline' = 'overlay';
  @Output() favoriteChange = new EventEmitter<boolean>();

  favorited = false;
  busy = false;

  private libraryService = inject(LibraryService);
  private toastService = inject(ToastService);
  private subscription?: Subscription;

  ngOnInit() {
    this.libraryService.ensureFavoriteIdsLoaded();
    this.syncFavorited();
    this.subscription = this.libraryService.favoriteIdsChanged$.subscribe(() => {
      this.syncFavorited();
    });
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['unitId'] && !changes['unitId'].firstChange) {
      this.syncFavorited();
    }
  }

  ngOnDestroy() {
    this.subscription?.unsubscribe();
  }

  private syncFavorited() {
    this.favorited = this.unitId > 0 && this.libraryService.isFavorited(this.unitId);
  }

  toggle(event: Event) {
    event.stopPropagation();
    event.preventDefault();
    if (this.busy || this.unitId <= 0) {
      return;
    }

    const next = !this.favorited;
    const unitId = this.unitId;
    this.busy = true;
    this.libraryService.setFavorite(unitId, next).subscribe({
      next: favorited => {
        this.busy = false;
        if (unitId === this.unitId) {
          this.favorited = favorited;
        }
        this.favoriteChange.emit(favorited);
        this.toastService.success(favorited ? 'Saved to favorites' : 'Removed from favorites');
      },
      error: () => {
        this.busy = false;
        this.toastService.error('Failed to update favorites');
      }
    });
  }
}
