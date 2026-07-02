import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { LibraryItemCardComponent } from '../../../components/library-item-card/library-item-card.component';
import { LibraryService } from '../../../services/library.service';
import { LibraryCryptoService } from '../../../services/crypto/library-crypto.service';
import { CrewService } from '../../../services/crew.service';
import { ToastService } from '../../../components/toast/toast.component';
import { EncryptionContentService } from '../../../services/encryption-content.service';
import { LibraryOfferingListItem, LibraryUnitListItem } from '../../../models/library.model';

@Component({
  selector: 'app-library-my-offerings',
  standalone: true,
  imports: [CommonModule, FormsModule, PageLayoutComponent, LibraryItemCardComponent],
  templateUrl: './library-my-offerings.component.html',
  styleUrl: './library-my-offerings.component.css'
})
export class LibraryMyOfferingsComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('loadMoreSentinel') loadMoreSentinel?: ElementRef<HTMLElement>;

  backButton!: ActionBarButton;
  displayItems: { offering: LibraryOfferingListItem; card: LibraryUnitListItem }[] = [];
  searchQuery = '';
  loading = true;
  loadingMore = false;
  hasMore = false;
  errorMessage = '';
  crewId = 0;

  private readonly pageSize = 30;
  private router = inject(Router);
  private libraryService = inject(LibraryService);
  private libraryCrypto = inject(LibraryCryptoService);
  private crewService = inject(CrewService);
  private toastService = inject(ToastService);
  private encryptionContent = inject(EncryptionContentService);
  private searchChanges$ = new Subject<string>();
  private destroy$ = new Subject<void>();
  private listObserver?: IntersectionObserver;
  private loadedOfferings: LibraryOfferingListItem[] = [];

  constructor() {
    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate(['/app/crew/library-of-things'])
    };
  }

  ngOnInit() {
    this.crewService.getMembership().subscribe({
      next: membership => {
        this.crewId = membership.crewId ?? 0;
        this.loadItems(true);
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Failed to load crew membership.';
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

  openItem(item: LibraryOfferingListItem) {
    if (item.unitId) {
      this.router.navigate(['/app/crew/library-of-things/units', item.unitId], {
        queryParams: { from: 'mine' }
      });
    }
  }

  editOffering(event: MouseEvent, offering: LibraryOfferingListItem) {
    event.stopPropagation();
    this.router.navigate(['/app/crew/library-of-things/offerings', offering.offeringId, 'edit']);
  }

  canEdit(offering: LibraryOfferingListItem): boolean {
    return offering.offeringKind === 'Consumable' || offering.offeringKind === 'Service';
  }

  kindLabel(kind: string): string {
    switch (kind) {
      case 'Consumable':
        return 'Consumable';
      case 'Service':
        return 'Service';
      default:
        return 'Durable';
    }
  }

  fulfillmentLabel(mode: string): string {
    return mode === 'OnDemand' ? 'On demand' : 'On request';
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
      this.displayItems = [];
      this.loadedOfferings = [];
      this.hasMore = false;
    } else {
      this.loadingMore = true;
    }

    this.libraryService.getMyOfferings({
      search: this.searchQuery,
      limit: this.pageSize,
      offset: reset ? 0 : this.loadedOfferings.length
    }).subscribe({
      next: page => {
        void this.applyPage(page, reset);
      },
      error: err => {
        this.loading = false;
        this.loadingMore = false;
        this.errorMessage = err?.message ?? 'Failed to load your offerings';
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

  private async applyPage(page: { items: LibraryOfferingListItem[]; hasMore: boolean }, reset: boolean) {
    const items = reset ? page.items : [...this.loadedOfferings, ...page.items];
    this.loadedOfferings = items;
    this.hasMore = page.hasMore;

    const listItems: LibraryUnitListItem[] = page.items
      .filter(item => item.unitId)
      .map(item => ({
        unitId: item.unitId!,
        offeringId: item.offeringId,
        holderUserId: 0,
        holderUsername: 'You',
        title: item.title,
        descriptionPreview: item.descriptionPreview,
        categories: item.categories,
        thumbnailResourceId: item.thumbnailResourceId,
        hasEncryptedContent: item.hasEncryptedContent,
        remainingStock: item.remainingStock,
        quantityNotApplicable: item.quantityNotApplicable,
        isOutOfStock: item.isOutOfStock,
        offeringKind: item.offeringKind,
        fulfillmentMode: item.fulfillmentMode
      }));

    let enriched = listItems;
    try {
      await this.encryptionContent.whenReady();
      if (this.crewId > 0) {
        enriched = await this.libraryCrypto.enrichUnitListItems(listItems, this.crewId);
      }
    } catch {
      enriched = listItems;
    }

    const enrichedByUnitId = new Map(enriched.map(item => [item.unitId, item]));
    const newRows = page.items.map(offering => ({
      offering,
      card: offering.unitId
        ? enrichedByUnitId.get(offering.unitId) ?? {
            unitId: offering.unitId,
            offeringId: offering.offeringId,
            holderUserId: 0,
            holderUsername: 'You',
            title: offering.title,
            descriptionPreview: offering.descriptionPreview,
            categories: offering.categories,
            remainingStock: offering.remainingStock,
            quantityNotApplicable: offering.quantityNotApplicable,
            isOutOfStock: offering.isOutOfStock,
            offeringKind: offering.offeringKind,
            fulfillmentMode: offering.fulfillmentMode
          }
        : {
            unitId: 0,
            offeringId: offering.offeringId,
            holderUserId: 0,
            holderUsername: 'You',
            title: offering.title,
            descriptionPreview: offering.descriptionPreview,
            categories: offering.categories,
            remainingStock: offering.remainingStock,
            quantityNotApplicable: offering.quantityNotApplicable,
            isOutOfStock: offering.isOutOfStock,
            offeringKind: offering.offeringKind,
            fulfillmentMode: offering.fulfillmentMode
          }
    }));

    this.displayItems = reset ? newRows : [...this.displayItems, ...newRows];
    this.loading = false;
    this.loadingMore = false;
    setTimeout(() => this.setupLoadMoreObserver(), 0);
  }
}
