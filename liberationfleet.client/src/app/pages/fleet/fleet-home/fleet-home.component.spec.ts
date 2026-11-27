import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { provideRouter, Router } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { FleetHomeComponent } from './fleet-home.component';
import { FleetService } from '../../../services/fleet.service';
import { GiftService } from '../../../services/gift.service';
import { NotificationService } from '../../../services/notification.service';
import { CryptoSessionService } from '../../../services/crypto/crypto-session.service';
import { EncryptedImageCacheService } from '../../../services/encrypted-image-cache.service';
import { ForumListPrefetchService } from '../../../services/forum-list-prefetch.service';
import { FleetDashboardPreviewService } from '../../../services/fleet-dashboard-preview.service';
import { ToastService } from '../../../components/toast/toast.component';
import { ContentBadgeComponent } from '../../../components/content-badge/content-badge.component';
import { emptyAreaCounts } from '../../../utils/notification-area.util';
import { CrewDashboardPreviews } from '../../../models/crew-dashboard-preview.model';

@Component({
  selector: 'app-nav-layout',
  standalone: true,
  template: '<ng-content></ng-content>'
})
class StubNavLayoutComponent {
  @Input() activeTab: string | null = null;
}

@Component({
  selector: 'app-hub-loading',
  standalone: true,
  template: ''
})
class StubHubLoadingComponent {
  @Input() variant: string | null = null;
  @Input() label = '';
}

@Component({
  selector: 'app-donation-campaign-widget',
  standalone: true,
  template: ''
})
class StubDonationCampaignWidgetComponent {
  @Input() variant: string | null = null;
  @Input() enabled = false;
}

@Component({
  selector: 'app-brand-logo',
  standalone: true,
  template: ''
})
class StubBrandLogoComponent {
  @Input() variant: string | null = null;
  @Input() size: string | null = null;
  @Input() customSrc: string | null = null;
  @Input() alt = '';
}

@Component({
  selector: 'app-crew-dashboard-preview',
  standalone: true,
  template: '<ng-content></ng-content>'
})
class StubCrewDashboardPreviewComponent {
  @Input() preview: unknown;
  @Input() loading = false;
  @Input() crewId: number | null = null;
  @Input() fleetId: number | null = null;
  @Output() open = new EventEmitter<unknown>();
  @Output() needMore = new EventEmitter<string>();
}

@Component({
  selector: 'app-collapsible-section',
  standalone: true,
  template: '<ng-content></ng-content>'
})
class StubCollapsibleSectionComponent {
  @Input() title = '';
  @Input() variant: string | null = null;
  @Input() expanded = false;
  @Output() expandedChange = new EventEmitter<boolean>();
}

@Component({
  selector: 'app-mute-duration-dialog',
  standalone: true,
  template: ''
})
class StubMuteDurationDialogComponent {
  @Input() visible = false;
  @Output() durationChosen = new EventEmitter<unknown>();
  @Output() cancelMute = new EventEmitter<void>();
  @Output() dismissed = new EventEmitter<void>();
}

@Component({
  selector: 'app-copy-text-button',
  standalone: true,
  template: ''
})
class StubCopyTextButtonComponent {
  @Input() value = '';
  @Input() label = '';
}

describe('FleetHomeComponent', () => {
  let fixture: ComponentFixture<FleetHomeComponent>;
  let component: FleetHomeComponent;
  let router: Router;
  let fleetService: {
    getStatus: jasmine.Spy;
    statusChanged$: BehaviorSubject<void>;
    peekPersistedStatus: jasmine.Spy;
    clearStatusCache: jasmine.Spy;
    getNextAid: jasmine.Spy;
    getCurrent: jasmine.Spy;
  };
  let dashboardPreviewService: {
    loadPreview: jasmine.Spy;
    peekCachedPreview: jasmine.Spy;
    appendPreviewItem: jasmine.Spy;
  };
  let unlocked$: BehaviorSubject<boolean>;

  beforeEach(async () => {
    unlocked$ = new BehaviorSubject(true);
    fleetService = {
      getStatus: jasmine.createSpy('getStatus').and.returnValue(of({
        hasFleet: true,
        fleetId: 9,
        fleetName: 'Harbor',
        libraryOfThingsEnabled: true
      })),
      statusChanged$: new BehaviorSubject<void>(undefined),
      peekPersistedStatus: jasmine.createSpy('peekPersistedStatus').and.returnValue(null),
      clearStatusCache: jasmine.createSpy('clearStatusCache'),
      getNextAid: jasmine.createSpy('getNextAid').and.returnValue(of({ success: true, nextAid: null })),
      getCurrent: jasmine.createSpy('getCurrent').and.returnValue(of({
        success: true,
        fleet: { libraryOfThingsEnabled: true }
      }))
    };
    dashboardPreviewService = {
      loadPreview: jasmine.createSpy('loadPreview').and.resolveTo(null),
      peekCachedPreview: jasmine.createSpy('peekCachedPreview').and.returnValue(undefined),
      appendPreviewItem: jasmine.createSpy('appendPreviewItem').and.resolveTo(null)
    };

    await TestBed.configureTestingModule({
      imports: [FleetHomeComponent],
      providers: [
        provideRouter([]),
        { provide: FleetService, useValue: fleetService },
        {
          provide: GiftService,
          useValue: { navigateToNextAidAction: jasmine.createSpy('navigateToNextAidAction') }
        },
        {
          provide: NotificationService,
          useValue: {
            areaCounts$: new BehaviorSubject(emptyAreaCounts()),
            refreshBadges: jasmine.createSpy('refreshBadges'),
            getMutes: jasmine.createSpy('getMutes').and.returnValue(of({ success: true, message: '', items: [] })),
            isMuted: jasmine.createSpy('isMuted').and.returnValue(false),
            setMute: jasmine.createSpy('setMute').and.returnValue(of({ success: true, message: '' }))
          }
        },
        {
          provide: CryptoSessionService,
          useValue: {
            unlocked$,
            isUnlocked: () => unlocked$.value
          }
        },
        {
          provide: EncryptedImageCacheService,
          useValue: { getDataUrl: jasmine.createSpy('getDataUrl').and.resolveTo(null) }
        },
        {
          provide: ForumListPrefetchService,
          useValue: { prefetchFleetSpace: jasmine.createSpy('prefetchFleetSpace') }
        },
        { provide: FleetDashboardPreviewService, useValue: dashboardPreviewService },
        {
          provide: ToastService,
          useValue: { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') }
        }
      ]
    })
      .overrideComponent(FleetHomeComponent, {
        set: {
          imports: [
            CommonModule,
            StubNavLayoutComponent,
            ContentBadgeComponent,
            StubDonationCampaignWidgetComponent,
            StubBrandLogoComponent,
            StubHubLoadingComponent,
            StubCollapsibleSectionComponent,
            StubCrewDashboardPreviewComponent,
            StubMuteDurationDialogComponent,
            StubCopyTextButtonComponent
          ]
        }
      })
      .compileComponents();

    fixture = TestBed.createComponent(FleetHomeComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate');
  });

  it('loads fleet preview tiles after unlock', fakeAsync(() => {
    fixture.detectChanges();
    tick();
    expect(dashboardPreviewService.loadPreview).toHaveBeenCalledWith(9, 'forum', { force: true });
    expect(dashboardPreviewService.loadPreview).toHaveBeenCalledWith(9, 'chat', { force: true });
    expect(dashboardPreviewService.loadPreview).toHaveBeenCalledWith(9, 'gift', { force: true });
    expect(dashboardPreviewService.loadPreview).toHaveBeenCalledWith(9, 'library', { force: true });
  }));

  it('opens fleet deep links from preview tiles', () => {
    component.openPreview({
      id: 12,
      authorUserId: 2,
      authorUsername: 'Maya',
      lastActivityAt: new Date(),
      title: 'Post',
      body: 'Hi'
    });
    expect(router.navigate).toHaveBeenCalledWith(['/app/fleet/forums', 12]);

    component.openPreview({
      roomId: 3,
      messageId: 44,
      authorUserId: 3,
      authorUsername: 'Bo',
      createdAt: new Date().toISOString(),
      body: 'Yo',
      hasAttachments: false
    });
    expect(router.navigate).toHaveBeenCalledWith(['/app/fleet/chats', 3], {
      queryParams: { messageId: 44 }
    });

    component.openPreview({
      id: 7,
      message: 'Gifted',
      categoryLabel: '',
      timestamp: new Date(),
      unverified: false
    });
    expect(router.navigate).toHaveBeenCalledWith(['/app/fleet/gift-log', 7]);

    component.openPreview({
      libraryKind: 'unit',
      unit: {
        unitId: 21,
        offeringId: 1,
        holderUserId: 1,
        holderUsername: 'Ada',
        title: 'Drill',
        descriptionPreview: '',
        categories: []
      }
    });
    expect(router.navigate).toHaveBeenCalledWith(['/app/fleet/library/units', 21]);
  });

  it('appends more preview items on needMore', fakeAsync(() => {
    const next: CrewDashboardPreviews['forum'] = {
      kind: 'forum',
      hasMore: true,
      items: [{
        id: 1,
        authorUserId: 1,
        authorUsername: 'A',
        lastActivityAt: new Date(),
        title: 'One',
        body: ''
      }]
    };
    dashboardPreviewService.appendPreviewItem.and.resolveTo(next);
    fixture.detectChanges();
    tick();
    component.onPreviewNeedMore('forum');
    tick();
    expect(dashboardPreviewService.appendPreviewItem).toHaveBeenCalledWith(9, 'forum');
    expect(component.previews.forum).toEqual(next);
  }));
});
