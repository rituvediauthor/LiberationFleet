import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../../../components/page-layout/page-layout.component';
import { LibraryItemCardComponent } from '../../../../components/library-item-card/library-item-card.component';
import { LibraryCategoryPickerComponent } from '../../../../components/library-category-picker/library-category-picker.component';
import { LibraryService } from '../../../../services/library.service';
import { FleetService } from '../../../../services/fleet.service';
import { ToastService } from '../../../../components/toast/toast.component';
import { LibraryCategory, LibraryOfferingKind, LibraryUnitListItem } from '../../../../models/library.model';
import { NavigationService } from '../../../../services/navigation.service';

@Component({
  selector: 'app-fleet-library-list',
  standalone: true,
  imports: [CommonModule, FormsModule, PageLayoutComponent, LibraryItemCardComponent, LibraryCategoryPickerComponent],
  templateUrl: './fleet-library-list.component.html',
  styleUrl: './fleet-library-list.component.css'
})
export class FleetLibraryListComponent implements OnInit, AfterViewInit, OnDestroy {
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
  pageTitle = 'Offerings';
  kind: LibraryOfferingKind = 'Durable';
  holderLabel = 'Holder';

  private readonly pageSize = 30;
  private navigation = inject(NavigationService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private fleetService = inject(FleetService);
  private libraryService = inject(LibraryService);
  private toastService = inject(ToastService);
  private searchChanges$ = new Subject<string>();
  private destroy$ = new Subject<void>();
  private listObserver?: IntersectionObserver;

  constructor() {
    this.backButton = this.navigation.createBackButton(['/app/fleet/library']);
  }

  ngOnInit() {
    const data = this.route.snapshot.data;
    this.pageTitle = (data['title'] as string) ?? this.pageTitle;
    this.kind = (data['kind'] as LibraryOfferingKind) ?? this.kind;
    this.holderLabel = this.kind === 'Service' ? 'Offered by' : this.kind === 'Consumable' ? 'From' : 'Holder';

    this.loadItems(true);

    this.libraryService.getCategories({ inUseOnly: true, kind: this.kind }).subscribe({
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
    const from =
      this.kind === 'Service' ? 'services' : this.kind === 'Consumable' ? 'consumable' : 'durable';
    this.router.navigate(['/app/fleet/library/units', item.unitId], {
      queryParams: { from }
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

    const options = {
      search: this.searchQuery,
      categoryIds: [...this.selectedCategoryIds],
      limit: this.pageSize,
      offset: reset ? 0 : this.items.length
    };

    const loader =
      this.kind === 'Durable'
        ? this.fleetService.getLibraryDurableUnits(options)
        : this.fleetService.getLibraryStockUnits(
            this.kind === 'Service' ? 'Service' : 'Consumable',
            options
          );

    loader.subscribe({
      next: page => {
        this.items = reset ? page.items : [...this.items, ...page.items];
        this.hasMore = page.hasMore;
        this.loading = false;
        this.loadingMore = false;
        setTimeout(() => this.setupLoadMoreObserver(), 0);
      },
      error: err => {
        this.loading = false;
        this.loadingMore = false;
        this.errorMessage = err?.message ?? 'Failed to load offerings';
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
}
