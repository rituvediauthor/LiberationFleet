import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, skip, takeUntil } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { LibraryItemCardComponent } from '../../../components/library-item-card/library-item-card.component';
import { LibraryCategoryPickerComponent } from '../../../components/library-category-picker/library-category-picker.component';
import { LibraryService } from '../../../services/library.service';
import { LibraryCryptoService } from '../../../services/crypto/library-crypto.service';
import { ToastService } from '../../../components/toast/toast.component';
import { LibraryCategory, LibraryUnitListItem } from '../../../models/library.model';
import { CrewService } from '../../../services/crew.service';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { NavigationService } from '../../../services/navigation.service';

@Component({
  selector: 'app-library-favorites',
  standalone: true,
  imports: [CommonModule, FormsModule, PageLayoutComponent, LibraryItemCardComponent, LibraryCategoryPickerComponent],
  templateUrl: './library-favorites.component.html',
  styleUrl: './library-favorites.component.css'
})
export class LibraryFavoritesComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('loadMoreSentinel') loadMoreSentinel?: ElementRef<HTMLElement>;

  backButton!: ActionBarButton;
  items: LibraryUnitListItem[] = [];
  categories: LibraryCategory[] = [];
  selectedCategoryIds: number[] = [];
  searchQuery = '';
  loading = true;
  loadingMore = false;
  hasMore = false;
  errorMessage = '';
  showFilters = false;
  crewId = 0;
  scope: 'crew' | 'fleet' = 'crew';

  private readonly pageSize = 30;
  private navigation = inject(NavigationService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private crewService = inject(CrewService);
  private encryptionContent = inject(EncryptionContentService);
  private toastService = inject(ToastService);
  private searchChanges$ = new Subject<string>();
  private destroy$ = new Subject<void>();
  private listObserver?: IntersectionObserver;

  constructor() {
    this.scope = this.route.snapshot.data['scope'] === 'fleet' ? 'fleet' : 'crew';
    this.backButton = this.navigation.createBackButton(
      this.scope === 'fleet' ? ['/app/fleet/library'] : ['/app/crew/library-of-things']
    );
  }

  ngOnInit() {
    this.libraryService.ensureFavoriteIdsLoaded();
    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
        this.loadItems(true);
      },
      error: () => {
        this.loading = false;
        this.toastService.error('Failed to load crew membership');
      }
    });

    this.libraryService.getCategories({ inUseOnly: true }).subscribe({
      next: categories => {
        this.categories = categories;
      },
      error: () => {
        this.toastService.error('Failed to load categories');
      }
    });

    this.searchChanges$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => this.loadItems(true));

    this.libraryService.favoriteIdsChanged$
      .pipe(skip(1), takeUntil(this.destroy$))
      .subscribe(() => {
        this.items = this.items.filter(item => this.libraryService.isFavorited(item.unitId));
      });
  }

  ngAfterViewInit() {
    this.setupLoadMoreObserver();
  }

  ngOnDestroy() {
    this.listObserver?.disconnect();
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSearchChange() {
    this.searchChanges$.next(this.searchQuery);
  }

  toggleFilters() {
    this.showFilters = !this.showFilters;
  }

  onCategoriesChange(categoryIds: number[]) {
    this.selectedCategoryIds = categoryIds;
    this.loadItems(true);
  }

  openItem(item: LibraryUnitListItem) {
    if (this.scope === 'fleet') {
      this.router.navigate(['/app/fleet/library/units', item.unitId], {
        queryParams: { from: 'favorites' }
      });
      return;
    }
    this.router.navigate(['/app/crew/library-of-things/units', item.unitId], {
      queryParams: { from: 'favorites' }
    });
  }

  private setupLoadMoreObserver() {
    this.listObserver?.disconnect();
    const sentinel = this.loadMoreSentinel?.nativeElement;
    if (!sentinel || !this.hasMore) {
      return;
    }

    this.listObserver = new IntersectionObserver(entries => {
      if (entries.some(entry => entry.isIntersecting)) {
        this.loadMore();
      }
    }, { threshold: 0.1 });

    this.listObserver.observe(sentinel);
  }

  private loadItems(reset: boolean) {
    if (reset) {
      this.loading = true;
      this.errorMessage = '';
      this.items = [];
      this.hasMore = false;
    } else {
      this.loadingMore = true;
    }

    this.libraryService.getFavorites({
      search: this.searchQuery,
      categoryIds: [...this.selectedCategoryIds],
      limit: this.pageSize,
      offset: reset ? 0 : this.items.length
    }).subscribe({
      next: page => {
        void this.applyPage(page, reset);
      },
      error: err => {
        this.loading = false;
        this.loadingMore = false;
        this.errorMessage = err?.message ?? 'Failed to load favorites';
        this.toastService.error(this.errorMessage);
      }
    });
  }

  private loadMore() {
    if (this.loading || this.loadingMore || !this.hasMore) {
      return;
    }
    this.loadItems(false);
  }

  private async applyPage(page: { items: LibraryUnitListItem[]; hasMore: boolean }, reset: boolean) {
    try {
      await this.encryptionContent.whenReady();
      const enriched = this.crewId > 0
        ? await this.libraryCrypto.enrichUnitListItems(page.items, this.crewId)
        : page.items;
      this.items = reset ? enriched : [...this.items, ...enriched];
      this.hasMore = page.hasMore;
    } catch {
      this.items = reset ? page.items : [...this.items, ...page.items];
      this.hasMore = page.hasMore;
    } finally {
      this.loading = false;
      this.loadingMore = false;
      setTimeout(() => this.setupLoadMoreObserver(), 0);
    }
  }
}
