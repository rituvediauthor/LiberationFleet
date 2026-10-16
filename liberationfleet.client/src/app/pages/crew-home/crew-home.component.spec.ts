import { discardPeriodicTasks, fakeAsync, tick, ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, provideRouter } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { CrewHomeComponent } from './crew-home.component';
import { CrewService } from '../../services/crew.service';
import { GiftService } from '../../services/gift.service';
import { CrewCryptoSyncService } from '../../services/crew-crypto-sync.service';
import { CryptoSessionService } from '../../services/crypto/crypto-session.service';
import { ProposalCryptoService } from '../../services/crypto/proposal-crypto.service';
import { EncryptedImageCacheService } from '../../services/encrypted-image-cache.service';
import { LibraryAccessService } from '../../services/library-access.service';
import { NotificationService } from '../../services/notification.service';
import { ForumListPrefetchService } from '../../services/forum-list-prefetch.service';
import { CrewDashboardPreviewService } from '../../services/crew-dashboard-preview.service';
import { ProfileService } from '../../services/profile.service';
import { ToastService } from '../../components/toast/toast.component';
import { ContentBadgeComponent } from '../../components/content-badge/content-badge.component';
import { createCrewServiceMock, createGiftServiceMock } from '../../testing/test-helpers';
import { emptyAreaCounts } from '../../utils/notification-area.util';
import { CrewDashboardPreview, CrewDashboardPreviews } from '../../models/crew-dashboard-preview.model';

@Component({
  selector: 'app-nav-layout',
  standalone: true,
  template: '<ng-content></ng-content>'
})
class StubNavLayoutComponent {
  @Input() activeTab = 'crew';
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
  @Input() alt = '';
  @Input() customSrc: string | null = null;
}

@Component({
  selector: 'app-hub-loading',
  standalone: true,
  template: ''
})
class StubHubLoadingComponent {
  @Input() variant: string | null = null;
  @Input() label: string | null = null;
}

@Component({
  selector: 'app-crew-dashboard-preview',
  standalone: true,
  imports: [CommonModule],
  template: `
    <ng-content></ng-content>
    <button type="button" class="stub-preview" *ngIf="preview" (click)="open.emit(preview)"></button>
  `
})
class StubCrewDashboardPreviewComponent {
  @Input() preview: CrewDashboardPreview | null = null;
  @Input() loading = false;
  @Input() crewId: number | null = null;
  @Output() open = new EventEmitter<CrewDashboardPreview>();
}

@Component({
  selector: 'app-crew-find-panel',
  standalone: true,
  template: `
    <ng-container *ngIf="loadError; else welcome">
      <p>Couldn't load your crew</p>
      <button type="button" class="menu-link" (click)="retry.emit()">Try again</button>
    </ng-container>
    <ng-template #welcome>
      <button type="button" class="menu-link" (click)="createCrew.emit()">Create Crew</button>
      <button type="button" class="menu-link" (click)="joinCrew.emit()">Join Crew</button>
      <button type="button" class="menu-link" (click)="invitations.emit()">My Invitations</button>
      <button type="button" class="menu-link" (click)="joinRequests.emit()">My Join Requests</button>
      <button type="button" class="menu-link" [disabled]="true" (click)="crewDashboard.emit()">Crew Dashboard</button>
      <button type="button" class="menu-link" (click)="howToUse.emit()">How to use this app</button>
    </ng-template>
  `,
  imports: [CommonModule]
})
class StubCrewFindPanelComponent {
  @Input() hasCrew = false;
  @Input() loadError = false;
  @Output() retry = new EventEmitter<void>();
  @Output() createCrew = new EventEmitter<void>();
  @Output() joinCrew = new EventEmitter<void>();
  @Output() invitations = new EventEmitter<void>();
  @Output() joinRequests = new EventEmitter<void>();
  @Output() crewDashboard = new EventEmitter<void>();
  @Output() howToUse = new EventEmitter<void>();
}

@Component({
  selector: 'app-collapsible-section',
  standalone: true,
  template: '<ng-content></ng-content>'
})
class StubCollapsibleSectionComponent {
  @Input() title = '';
  @Input() variant: string | null = null;
}

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  template: ''
})
class StubConfirmDialogComponent {
  @Input() visible = false;
  @Input() title = '';
  @Input() message = '';
  @Input() confirmLabel = '';
  @Input() cancelLabel = '';
  @Output() confirmed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();
  @Output() dismissed = new EventEmitter<void>();
}

@Component({
  selector: 'app-char-counter',
  standalone: true,
  template: ''
})
class StubCharCounterComponent {
  @Input() current = 0;
  @Input() max = 0;
}

describe('CrewHomeComponent', () => {
  let fixture: ComponentFixture<CrewHomeComponent>;
  let component: CrewHomeComponent;
  let crewService: jasmine.SpyObj<CrewService>;
  let giftService: jasmine.SpyObj<GiftService>;
  let router: Router;
  let unlocked$: BehaviorSubject<boolean>;
  let cryptoUnlocked = false;
  let areaCounts$: BehaviorSubject<ReturnType<typeof emptyAreaCounts>>;
  let notificationService: { areaCounts$: BehaviorSubject<ReturnType<typeof emptyAreaCounts>>; refreshBadges: jasmine.Spy };
  let dashboardPreviewService: { loadPreviews: jasmine.Spy; loadPreview: jasmine.Spy };

  beforeEach(async () => {
    crewService = createCrewServiceMock();
    giftService = createGiftServiceMock();
    crewService.getMembership.and.returnValue(of({ hasCrew: false }));
    giftService.getSeasonStatus.and.returnValue(of({
      seasonStarted: true,
      userInSeason: true,
      userSeasonReady: true,
      readyCount: 3,
      canStartSeason: false
    }));
    giftService.getNextAidInfo.and.returnValue(of({
      recipientName: 'Ritu',
      amount: 20,
      platformDisplayKind: 'preferred',
      platformName: 'Venmo',
      platformHandle: '@ritu'
    }));

    cryptoUnlocked = false;
    unlocked$ = new BehaviorSubject(false);
    areaCounts$ = new BehaviorSubject(emptyAreaCounts());
    notificationService = {
      areaCounts$,
      refreshBadges: jasmine.createSpy('refreshBadges')
    };
    dashboardPreviewService = {
      loadPreviews: jasmine.createSpy('loadPreviews').and.resolveTo({
        forum: null,
        chat: null,
        gift: null,
        library: null
      } satisfies CrewDashboardPreviews),
      loadPreview: jasmine.createSpy('loadPreview').and.resolveTo(null)
    };

    await TestBed.configureTestingModule({
      imports: [CrewHomeComponent],
      providers: [
        provideRouter([]),
        { provide: CrewService, useValue: crewService },
        { provide: GiftService, useValue: giftService },
        {
          provide: CrewCryptoSyncService,
          useValue: jasmine.createSpyObj<CrewCryptoSyncService>('CrewCryptoSyncService', [
            'syncActiveCrewKeyDistributions'
          ])
        },
        {
          provide: CryptoSessionService,
          useValue: {
            unlocked$,
            isUnlocked: () => cryptoUnlocked
          }
        },
        {
          provide: EncryptedImageCacheService,
          useValue: jasmine.createSpyObj<EncryptedImageCacheService>('EncryptedImageCacheService', [
            'getDataUrl'
          ])
        },
        {
          provide: LibraryAccessService,
          useValue: jasmine.createSpyObj<LibraryAccessService>('LibraryAccessService', [
            'navigateToLibrary'
          ])
        },
        {
          provide: NotificationService,
          useValue: notificationService
        },
        {
          provide: ForumListPrefetchService,
          useValue: jasmine.createSpyObj<ForumListPrefetchService>('ForumListPrefetchService', [
            'prefetchCrewSpace'
          ])
        },
        {
          provide: CrewDashboardPreviewService,
          useValue: dashboardPreviewService
        },
        {
          provide: ProposalCryptoService,
          useValue: jasmine.createSpyObj<ProposalCryptoService>('ProposalCryptoService', [
            'encryptCommentPayload'
          ])
        },
        {
          provide: ProfileService,
          useValue: jasmine.createSpyObj<ProfileService>('ProfileService', ['getProfile'])
        },
        {
          provide: ToastService,
          useValue: jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error'])
        }
      ]
    })
      .overrideComponent(CrewHomeComponent, {
        set: {
          imports: [
            CommonModule,
            FormsModule,
            RouterLink,
            StubNavLayoutComponent,
            ContentBadgeComponent,
            StubDonationCampaignWidgetComponent,
            StubBrandLogoComponent,
            StubHubLoadingComponent,
            StubCrewDashboardPreviewComponent,
            StubCrewFindPanelComponent,
            StubCollapsibleSectionComponent,
            StubConfirmDialogComponent,
            StubCharCounterComponent
          ]
        }
      })
      .compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(CrewHomeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load membership on init', () => {
    expect(component).toBeTruthy();
    expect(crewService.getMembership).toHaveBeenCalled();
    expect(component.loading).toBeFalse();
    expect(component.membership?.hasCrew).toBeFalse();
  });

  it('should show welcome actions when user has no crew', () => {
    const buttons = fixture.nativeElement.querySelectorAll('.menu-link');
    expect(buttons.length).toBe(6);
    expect(buttons[0].textContent).toContain('Create Crew');
    expect(buttons[1].textContent).toContain('Join Crew');
    expect(buttons[2].textContent).toContain('My Invitations');
    expect(buttons[3].textContent).toContain('My Join Requests');
    expect(buttons[4].textContent).toContain('Crew Dashboard');
    expect(buttons[4].disabled).toBeTrue();
    expect(buttons[5].textContent).toContain('How to use this app');
  });

  it('should show retry instead of create/join when membership fails to load', () => {
    crewService.getMembership.and.returnValue(throwError(() => new Error('membership failed')));

    fixture = TestBed.createComponent(CrewHomeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.loadError).toBeTrue();
    expect(component.membership).toBeNull();
    const buttons = fixture.nativeElement.querySelectorAll('.menu-link');
    expect(buttons.length).toBe(1);
    expect(buttons[0].textContent).toContain('Try again');
    expect(fixture.nativeElement.textContent).toContain("Couldn't load your crew");
  });

  it('should retry membership by clearing the session cache', () => {
    component.loadError = true;
    component.loading = false;
    component.retryMembership();
    expect(component.loading).toBeTrue();
    expect(component.loadError).toBeFalse();
    expect(crewService.clearMembershipCache).toHaveBeenCalled();
  });

  it('should show crew dashboard when user has a crew', () => {
    crewService.getMembership.and.returnValue(of({
      hasCrew: true,
      crewId: 1,
      crewName: 'Alpha Fleet',
      joinCode: 'ALPHA123',
      seasonStarted: true
    }));
    component.ngOnInit();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.crew-name-link .menu-label')?.textContent).toContain('Alpha Fleet');
    expect(fixture.nativeElement.querySelector('.info-text')?.textContent).toContain('Ritu needs $20');
    expect(fixture.nativeElement.querySelector('.info-platform')?.textContent).toContain('Venmo: @ritu');
    // Rules + crewspace + chats + giving + library + proposals + crewmates + settings
    expect(fixture.nativeElement.querySelectorAll('.menu-link').length).toBe(8);
  });

  it('should navigate to gift log', () => {
    component.membership = {
      hasCrew: true,
      crewId: 1,
      seasonStarted: true,
      isInSeason: true
    };
    component.goToGiftLog();
    expect(giftService.navigateToGiftLogEntry).toHaveBeenCalledWith(router, true);
  });

  it('should navigate to create crew page', () => {
    component.goToCreateCrew();
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/create']);
  });

  it('should navigate to join crew page', () => {
    component.goToJoinCrew();
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/join']);
  });

  it('should route the chats menu button to the chat list page', fakeAsync(() => {
    crewService.getMembership.and.returnValue(of({
      hasCrew: true,
      crewId: 1,
      crewName: 'Alpha Fleet',
      joinCode: 'ALPHA123',
      seasonStarted: true
    }));

    const navigateByUrlSpy = spyOn(router, 'navigateByUrl').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(CrewHomeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const chatsButton = Array.from(fixture.nativeElement.querySelectorAll('.menu-link') as NodeListOf<Element>)
      .find(button => button.textContent?.includes('Chats')) as HTMLButtonElement | undefined;

    expect(chatsButton).toBeDefined();

    chatsButton?.click();
    tick();

    expect(navigateByUrlSpy).toHaveBeenCalled();
    fixture.destroy();
    discardPeriodicTasks();
  }));

  it('should defer the cycle thank-you prompt until encryption is unlocked', () => {
    crewService.getMembership.and.returnValue(of({
      hasCrew: true,
      crewId: 1,
      crewName: 'Alpha Fleet',
      seasonStarted: true,
      pendingCycleThankYouGiftId: 42
    }));

    fixture = TestBed.createComponent(CrewHomeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.showCycleThankYouPrompt).toBeFalse();
    expect(component.cycleThankYouGiftId).toBe(42);

    cryptoUnlocked = true;
    unlocked$.next(true);
    fixture.detectChanges();

    expect(component.showCycleThankYouPrompt).toBeTrue();
  });

  it('polls badge freshness and only refreshes the preview tiles whose counts changed', fakeAsync(() => {
    crewService.getMembership.and.returnValue(of({
      hasCrew: true,
      crewId: 1,
      crewName: 'Alpha Fleet',
      seasonStarted: true
    }));

    fixture = TestBed.createComponent(CrewHomeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    tick();

    expect(dashboardPreviewService.loadPreviews).toHaveBeenCalledWith(1);

    tick(5000);
    expect(notificationService.refreshBadges).toHaveBeenCalledWith(true);

    areaCounts$.next({
      ...emptyAreaCounts(),
      crewChats: 2,
      crewLibrary: 1
    });
    tick();

    expect(dashboardPreviewService.loadPreview).toHaveBeenCalledWith(1, 'chat', { force: true });
    expect(dashboardPreviewService.loadPreview).toHaveBeenCalledWith(1, 'library', { force: true });
    expect(dashboardPreviewService.loadPreview).not.toHaveBeenCalledWith(1, 'forum', { force: true });
    expect(dashboardPreviewService.loadPreview).not.toHaveBeenCalledWith(1, 'gift', { force: true });

    fixture.destroy();
    discardPeriodicTasks();
  }));

  it('should open a forum preview deep link', () => {
    component.openPreview({
      kind: 'forum',
      id: 55,
      authorUsername: 'Ada',
      lastActivityAt: new Date(),
      title: 'Post',
      body: 'Hi'
    });
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/forums', 55]);
  });

  it('should open a chat message preview deep link', () => {
    component.openPreview({
      kind: 'chat',
      roomId: 8,
      messageId: 99,
      authorUsername: 'Bo',
      createdAt: new Date().toISOString(),
      body: 'Yo',
      hasAttachments: false
    });
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/chats', 8], {
      queryParams: { messageId: 99 }
    });
  });

  it('should open library unit and quest preview deep links', () => {
    component.openPreview({
      libraryKind: 'unit',
      unit: {
        unitId: 3,
        offeringId: 1,
        holderUserId: 1,
        holderUsername: 'Ada',
        title: 'Drill',
        descriptionPreview: 'Cordless',
        categories: []
      }
    });
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/library-of-things/units', 3]);

    component.openPreview({
      libraryKind: 'quest',
      task: {
        taskId: 6,
        title: 'Paint',
        creatorUsername: 'Kai',
        creatorUserId: 2,
        value: 10,
        scheduleSummary: 'Soon'
      },
      dueLabel: 'Soon'
    });
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/library-of-things/tasks', 6]);
  });
});
