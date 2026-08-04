import { Component, DestroyRef, Input, OnChanges, OnInit, SimpleChanges, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { FallibleFooterComponent } from '../fallible-footer/fallible-footer.component';
import { BrandLogoComponent } from '../brand-logo/brand-logo.component';
import { LocationHeaderComponent } from '../location-header/location-header.component';
import {
  LocationHeaderInfo,
  ParentTab,
  isParentTab,
  parentTabLabel,
  parentTabPath,
  resolveLocationHeader
} from '../../utils/location-header.util';

export interface ActionBarButton {
  label: string;
  type: 'back' | 'primary' | 'secondary';
  disabled?: boolean;
  onClick?: () => void;
}

@Component({
  selector: 'app-page-layout',
  standalone: true,
  imports: [CommonModule, FallibleFooterComponent, BrandLogoComponent, LocationHeaderComponent],
  templateUrl: './page-layout.component.html',
  styleUrl: './page-layout.component.css'
})
export class PageLayoutComponent implements OnInit, OnChanges {
  @Input() backButton: ActionBarButton | null = null;
  @Input() primaryButton: ActionBarButton | null = null;
  @Input() secondaryButton: ActionBarButton | null = null;
  @Input() fillHeight = false;
  @Input() brandNavButton = false;
  /** Attribution + donate strip. Hide on discourse/comms and create/edit forms. */
  @Input() showFallibleFooter = true;
  /** Optional override; otherwise resolved from route data.parentTab + data.locationHeader. */
  @Input() parentTab: ParentTab | null = null;
  @Input() locationHeader: string | null = null;
  /** Set false to hide even when route data provides a header (e.g. nested conversation chrome). */
  @Input() showLocationHeader = true;

  locationHeaderInfo: LocationHeaderInfo | null = null;

  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);

  ngOnInit() {
    this.refreshLocationHeader();
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => this.refreshLocationHeader());
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['parentTab'] || changes['locationHeader'] || changes['showLocationHeader']) {
      this.refreshLocationHeader();
    }
  }

  get showCrewFallback(): boolean {
    return !this.backButton
      && !this.primaryButton
      && !this.secondaryButton
      && !this.brandNavButton;
  }

  onBrandNavClick() {
    this.router.navigate(['/']);
  }

  goToCrewHome() {
    void this.router.navigate(['/app/crew']);
  }

  private refreshLocationHeader() {
    if (!this.showLocationHeader) {
      this.locationHeaderInfo = null;
      return;
    }

    if (this.parentTab && this.locationHeader?.trim()) {
      this.locationHeaderInfo = {
        parentTab: this.parentTab,
        parentLabel: parentTabLabel(this.parentTab),
        parentPath: parentTabPath(this.parentTab),
        pageLabel: this.locationHeader.trim()
      };
      return;
    }

    const fromRoute = resolveLocationHeader(this.route.snapshot.root);
    if (fromRoute && (!this.parentTab || fromRoute.parentTab === this.parentTab)) {
      if (this.locationHeader?.trim()) {
        this.locationHeaderInfo = {
          ...fromRoute,
          pageLabel: this.locationHeader.trim()
        };
        return;
      }
      this.locationHeaderInfo = fromRoute;
      return;
    }

    if (isParentTab(this.parentTab) && this.locationHeader?.trim()) {
      this.locationHeaderInfo = {
        parentTab: this.parentTab,
        parentLabel: parentTabLabel(this.parentTab),
        parentPath: parentTabPath(this.parentTab),
        pageLabel: this.locationHeader.trim()
      };
      return;
    }

    this.locationHeaderInfo = fromRoute;
  }
}
