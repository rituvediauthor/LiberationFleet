import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, provideRouter } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { CrewHomeComponent } from './crew-home.component';
import { CrewService } from '../../services/crew.service';
import { GiftService } from '../../services/gift.service';
import { CrewCryptoSyncService } from '../../services/crew-crypto-sync.service';
import { CryptoSessionService } from '../../services/crypto/crypto-session.service';
import { EncryptedImageCacheService } from '../../services/encrypted-image-cache.service';
import { LibraryAccessService } from '../../services/library-access.service';
import { NotificationService } from '../../services/notification.service';
import { ForumListPrefetchService } from '../../services/forum-list-prefetch.service';
import { ContentBadgeComponent } from '../../components/content-badge/content-badge.component';
import { createCrewServiceMock, createGiftServiceMock } from '../../testing/test-helpers';
import { emptyAreaCounts } from '../../utils/notification-area.util';

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

describe('CrewHomeComponent', () => {
  let fixture: ComponentFixture<CrewHomeComponent>;
  let component: CrewHomeComponent;
  let crewService: jasmine.SpyObj<CrewService>;
  let giftService: jasmine.SpyObj<GiftService>;
  let router: Router;

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

    const unlocked$ = new BehaviorSubject(false);
    const areaCounts$ = new BehaviorSubject(emptyAreaCounts());

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
            isUnlocked: () => false
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
          useValue: {
            areaCounts$,
            refreshBadges: jasmine.createSpy('refreshBadges')
          }
        },
        {
          provide: ForumListPrefetchService,
          useValue: jasmine.createSpyObj<ForumListPrefetchService>('ForumListPrefetchService', [
            'prefetchCrewSpace'
          ])
        }
      ]
    })
      .overrideComponent(CrewHomeComponent, {
        set: {
          imports: [
            CommonModule,
            RouterLink,
            StubNavLayoutComponent,
            ContentBadgeComponent,
            StubDonationCampaignWidgetComponent,
            StubBrandLogoComponent,
            StubHubLoadingComponent
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
    const buttons = fixture.nativeElement.querySelectorAll('.action-btn');
    expect(buttons.length).toBe(4);
    expect(buttons[0].textContent).toContain('Create Crew');
    expect(buttons[1].textContent).toContain('Join Crew');
    expect(buttons[2].textContent).toContain('My Invitations');
    expect(buttons[3].textContent).toContain('My Join Requests');
  });

  it('should show retry instead of create/join when membership fails to load', () => {
    crewService.getMembership.and.returnValue(throwError(() => new Error('membership failed')));

    fixture = TestBed.createComponent(CrewHomeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.loadError).toBeTrue();
    expect(component.membership).toBeNull();
    const buttons = fixture.nativeElement.querySelectorAll('.action-btn');
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
    expect(fixture.nativeElement.querySelectorAll('.menu-link').length).toBe(9);
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

  it('should route the chats menu button to the chat list page', async () => {
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
    await fixture.whenStable();

    expect(navigateByUrlSpy).toHaveBeenCalled();
  });
});
