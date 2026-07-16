import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { RecordGiftComponent } from './record-gift.component';
import { GiftService } from '../../services/gift.service';
import { CrewService } from '../../services/crew.service';
import { ProfileService } from '../../services/profile.service';
import { AuthService } from '../../services/auth.service';
import { NavigationService } from '../../services/navigation.service';
import { ToastService } from '../../components/toast/toast.component';
import {
  createAuthServiceMock,
  createCrewServiceMock,
  createGiftServiceMock,
  createProfileServiceMock,
  createToastServiceMock
} from '../../testing/test-helpers';

describe('RecordGiftComponent', () => {
  let fixture: ComponentFixture<RecordGiftComponent>;
  let component: RecordGiftComponent;
  let giftService: jasmine.SpyObj<GiftService>;
  let toastService: jasmine.SpyObj<ToastService>;
  let router: Router;

  beforeEach(async () => {
    giftService = createGiftServiceMock();
    toastService = createToastServiceMock();
    const crewService = createCrewServiceMock();
    const profileService = createProfileServiceMock();
    const navigation = jasmine.createSpyObj<NavigationService>('NavigationService', ['createBackButton', 'back']);
    navigation.createBackButton.and.returnValue({
      label: 'back',
      type: 'back',
      onClick: () => undefined
    });

    giftService.getReceptionOrder.and.returnValue(of([]));
    giftService.getCrewMembers.and.returnValue(of([
      { id: 2, username: 'Ritu', platformIds: [1] },
      { id: 3, username: 'Ruth', platformIds: [2] }
    ]));
    giftService.recordGifts.and.returnValue(of({ success: true, message: 'Gifts recorded.' }));

    crewService.getPaymentPlatforms.and.returnValue(of([
      { id: 1, name: 'PayPal' },
      { id: 2, name: 'Cash App' }
    ]));

    profileService.getProfile.and.returnValue(of({
      id: 1,
      username: 'James',
      email: 'james@example.com',
      paymentPlatforms: [{ id: 1, platformId: 1, platform: 'PayPal', handle: 'james' }],
      roles: [],
      inNeedOfAid: false,
      emergencyLevel: 0,
      peopleRepresentedCount: 1,
      disabilityLevel: 0,
      needsSurvivalAid: false,
      isSurvivalThresholdRecipient: false,
      stats: {
        sacrificeCountLastSeason: 0,
        averageMonthlyContributions: 0,
        membershipStatus: true,
        lifetimeContributions: 0,
        receptionThisYear: 0,
        percentBoost: 0,
        priorityScore: 0
      }
    }));

    const authService = createAuthServiceMock();
    Object.defineProperty(authService, 'currentUser$', {
      value: of({ id: 1, username: 'James', email: 'james@example.com' })
    });

    await TestBed.configureTestingModule({
      imports: [RecordGiftComponent, HttpClientTestingModule],
      providers: [
        provideRouter([]),
        { provide: GiftService, useValue: giftService },
        { provide: CrewService, useValue: crewService },
        { provide: ProfileService, useValue: profileService },
        { provide: AuthService, useValue: authService },
        { provide: NavigationService, useValue: navigation },
        { provide: ToastService, useValue: toastService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(RecordGiftComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create and load reception order', () => {
    expect(component).toBeTruthy();
    expect(giftService.getReceptionOrder).toHaveBeenCalled();
    expect(component.loading).toBeFalse();
  });

  it('should enable record when a custom gift row is complete', () => {
    component.activeUserId = 1;
    component.form.patchValue({
      customRecipientId: '2',
      customAmount: 25,
      customPaymentPlatformId: '1'
    });
    fixture.detectChanges();

    expect(component.recordButton.disabled).toBeFalse();
  });

  it('should record custom gifts via recordGifts', () => {
    component.activeUserId = 1;
    component.form.patchValue({
      customRecipientId: '2',
      customAmount: 25,
      customPaymentPlatformId: '1'
    });

    component.onConfirmRecord();

    expect(giftService.recordGifts).toHaveBeenCalledWith([
      jasmine.objectContaining({
        amount: 25,
        paymentPlatformId: 1,
        recipientId: 2,
        isCustom: true
      })
    ]);
    expect(toastService.success).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/gift-log']);
  });
});
