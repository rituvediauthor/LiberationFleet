import { Component, EventEmitter, Input, OnDestroy, OnInit, Output, inject } from '@angular/core';
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
export class LibraryFavoriteButtonComponent implements OnInit, OnDestroy {
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
    this.favorited = this.libraryService.isFavorited(this.unitId);
    this.subscription = this.libraryService.favoriteIdsChanged$.subscribe(() => {
      this.favorited = this.libraryService.isFavorited(this.unitId);
    });
  }

  ngOnDestroy() {
    this.subscription?.unsubscribe();
  }

  toggle(event: Event) {
    event.stopPropagation();
    event.preventDefault();
    if (this.busy || this.unitId <= 0) {
      return;
    }

    const next = !this.favorited;
    this.busy = true;
    this.libraryService.setFavorite(this.unitId, next).subscribe({
      next: favorited => {
        this.busy = false;
        this.favorited = favorited;
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
