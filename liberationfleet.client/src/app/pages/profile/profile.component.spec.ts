import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ProfileComponent } from './profile.component';
import { AuthService } from '../../services/auth.service';
import { ProfileService } from '../../services/profile.service';
import { GiftService } from '../../services/gift.service';
import { ToastService } from '../../components/toast/toast.component';
import {
  createAuthServiceMock,
  createGiftServiceMock,
  createProfileServiceMock,
  createToastServiceMock
} from '../../testing/test-helpers';

describe('ProfileComponent', () => {
  let fixture: ComponentFixture<ProfileComponent>;
  let component: ProfileComponent;
  let profileService: jasmine.SpyObj<ProfileService>;
  let giftService: jasmine.SpyObj<GiftService>;
  let toastService: jasmine.SpyObj<ToastService>;
  let authService: jasmine.SpyObj<AuthService>;

  const mockProfile = {
    id: 4,
    username: 'James',
    email: 'james@example.com',
    paymentPlatforms: [{ id: 1, platformId: 1, platform: 'PayPal', handle: 'james@example.com' }],
    roles: ['Organizer'],
    inNeedOfAid: true,
    emergencyLevel: 0,
    needsSurvivalAid: false,
    isSurvivalThresholdRecipient: false,
    stats: {
      sacrificeCountLastSeason: 0,
      averageMonthlyContributions: 0,
      membershipStatus: false,
      lifetimeContributions: 0,
      receptionThisYear: 0,
      percentBoost: 0,
      priorityScore: 0
    }
  };

  beforeEach(async () => {
    profileService = createProfileServiceMock();
    giftService = createGiftServiceMock();
    toastService = createToastServiceMock();
    authService = createAuthServiceMock();

    giftService.getPaymentPlatforms.and.returnValue(of([
      { id: 1, name: 'PayPal' },
      { id: 3, name: 'Venmo' }
    ]));
    profileService.getProfile.and.returnValue(of(mockProfile));
    profileService.updateProfile.and.returnValue(of({
      success: true,
      message: 'Profile updated successfully',
      profile: {
        ...mockProfile,
        username: 'JamesUpdated',
        inNeedOfAid: false,
        emergencyLevel: 2,
        needsSurvivalAid: true
      }
    }));
    profileService.addPaymentPlatform.and.callFake((profile) => {
      profile.paymentPlatforms = [...profile.paymentPlatforms, { id: -1, platformId: 1, platform: 'PayPal', handle: '' }];
      return profile.paymentPlatforms.at(-1)!;
    });

    await TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authService },
        { provide: ProfileService, useValue: profileService },
        { provide: GiftService, useValue: giftService },
        { provide: ToastService, useValue: toastService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load profile from API', () => {
    expect(component).toBeTruthy();
    expect(profileService.getProfile).toHaveBeenCalled();
    expect(component.profile?.username).toBe('James');
  });

  it('should keep save disabled until profile changes', () => {
    expect(component.saveButton.disabled).toBeTrue();
  });

  it('should save all edited profile fields to the API', () => {
    component.form.patchValue({
      username: 'JamesUpdated',
      email: 'james.updated@example.com',
      inNeedOfAid: false,
      emergencyLevel: 2,
      needsSurvivalAid: true
    });
    component['updateSaveButton']();
    component.onSave();

    expect(profileService.updateProfile).toHaveBeenCalledWith({
      username: 'JamesUpdated',
      email: 'james.updated@example.com',
      inNeedOfAid: false,
      emergencyLevel: 2,
      needsSurvivalAid: true,
      paymentPlatforms: [{ id: 1, platformId: 1, platform: 'PayPal', handle: 'james@example.com' }]
    });
    expect(authService.updateCurrentUser).toHaveBeenCalledWith({
      id: 4,
      username: 'JamesUpdated',
      email: 'james@example.com'
    });
    expect(toastService.success).toHaveBeenCalled();
  });
});
