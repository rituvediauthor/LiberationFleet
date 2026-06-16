import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { RecordGiftComponent } from './record-gift.component';
import { GiftService } from '../../services/gift.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../components/toast/toast.component';
import {
  createAuthServiceMock,
  createGiftServiceMock,
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

    giftService.getCrewMembers.and.returnValue(of([
      { id: 2, username: 'Ritu' },
      { id: 3, username: 'Ruth' }
    ]));
    giftService.getPendingMiddlemanGifts.and.returnValue(of([
      { id: 10, initiatorId: 2, initiatorName: 'Ritu', recipientId: 3, recipientName: 'Ruth', amount: 30, platform: 'Cash App' }
    ]));
    giftService.recordGift.and.returnValue(of({ success: true, message: 'Gift recorded.' }));

    const authService = createAuthServiceMock();
    Object.defineProperty(authService, 'currentUser$', {
      value: of({ id: 1, username: 'James', email: 'james@example.com' })
    });

    await TestBed.configureTestingModule({
      imports: [RecordGiftComponent],
      providers: [
        provideRouter([]),
        { provide: GiftService, useValue: giftService },
        { provide: AuthService, useValue: authService },
        { provide: ToastService, useValue: toastService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(RecordGiftComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should enable log gift when direct gift form is valid', () => {
    component.form.patchValue({
      amount: 25,
      recipientId: '2',
      paymentPlatformId: 1
    });

    expect(component.logButton.disabled).toBeFalse();
  });

  it('should record a direct gift with all relevant fields', () => {
    component.form.patchValue({
      amount: 25,
      recipientId: '2',
      paymentPlatformId: 1
    });

    component.onConfirmLog();

    expect(giftService.recordGift).toHaveBeenCalledWith({
      amount: 25,
      paymentPlatformId: 1,
      recipientId: 2,
      middlemanId: undefined
    });
    expect(toastService.success).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/gift-log']);
  });

  it('should clear recipient when completing as middleman and send only completingGiftId', () => {
    component.form.patchValue({
      amount: 25,
      recipientId: '2',
      paymentPlatformId: 1
    });
    component.form.patchValue({ completingAsMiddleman: true });
    component.form.patchValue({
      pendingGiftId: '10',
      amount: 30,
      paymentPlatformId: 3
    });

    component.onConfirmLog();

    expect(giftService.recordGift).toHaveBeenCalledWith({
      amount: 30,
      paymentPlatformId: 3,
      completingGiftId: 10
    });
  });

  it('should record an initiated gift with middleman', () => {
    component.form.patchValue({
      amount: 40,
      recipientId: '2',
      useMiddleman: true,
      middlemanId: '3',
      paymentPlatformId: 2
    });
    component.onConfirmLog();

    expect(giftService.recordGift).toHaveBeenCalledWith({
      amount: 40,
      paymentPlatformId: 2,
      recipientId: 2,
      middlemanId: 3
    });
  });
});
