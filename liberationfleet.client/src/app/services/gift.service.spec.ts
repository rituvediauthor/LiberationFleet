import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { GiftService } from './gift.service';

describe('GiftService', () => {
  let service: GiftService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [GiftService]
    });

    service = TestBed.inject(GiftService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should load payment platforms from the API', () => {
    service.getPaymentPlatforms().subscribe(platforms => {
      expect(platforms.length).toBe(2);
      expect(platforms[0].name).toBe('PayPal');
    });

    const req = httpMock.expectOne('/api/payment-platforms');
    expect(req.request.method).toBe('GET');
    req.flush([
      { id: 1, name: 'PayPal' },
      { id: 3, name: 'Venmo' }
    ]);
  });

  it('should load gift log entries from the API', () => {
    service.getLogs().subscribe(entries => {
      expect(entries.length).toBe(1);
      expect(entries[0].message).toBe('James gave $30 to Ritu via PayPal');
      expect(entries[0].timestamp instanceof Date).toBeTrue();
    });

    const req = httpMock.expectOne('/api/gifts/log');
    expect(req.request.method).toBe('GET');
    req.flush({
      success: true,
      message: 'Gift log loaded.',
      items: [{
        id: 1,
        type: 'direct',
        giverId: 1,
        giverName: 'James',
        recipientId: 2,
        recipientName: 'Ritu',
        amount: 30,
        platform: 'PayPal',
        timestamp: '2026-06-14T12:00:00Z',
        message: 'James gave $30 to Ritu via PayPal',
        relatedUserIds: [1, 2]
      }]
    });
  });

  it('should record a gift through the API', () => {
    service.recordGift({
      amount: 25,
      recipientId: 2,
      paymentPlatformId: 3
    }).subscribe(result => {
      expect(result.success).toBeTrue();
    });

    const req = httpMock.expectOne('/api/gifts');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      amount: 25,
      paymentPlatformId: 3,
      recipientId: 2,
      middlemanId: null,
      completingGiftId: null
    });
    req.flush({ success: true, message: 'Gift recorded.' });
  });

  it('should record a gift with middleman without completingGiftId', () => {
    service.recordGift({
      amount: 40,
      recipientId: 2,
      middlemanId: 3,
      paymentPlatformId: 2
    }).subscribe(result => {
      expect(result.success).toBeTrue();
    });

    const req = httpMock.expectOne('/api/gifts');
    expect(req.request.body).toEqual({
      amount: 40,
      paymentPlatformId: 2,
      recipientId: 2,
      middlemanId: 3,
      completingGiftId: null
    });
    req.flush({ success: true, message: 'Gift initiated.' });
  });

  it('should record a completed middleman gift without recipientId', () => {
    service.recordGift({
      amount: 30,
      completingGiftId: 10,
      paymentPlatformId: 3
    }).subscribe(result => {
      expect(result.success).toBeTrue();
    });

    const req = httpMock.expectOne('/api/gifts');
    expect(req.request.body).toEqual({
      amount: 30,
      paymentPlatformId: 3,
      recipientId: null,
      middlemanId: null,
      completingGiftId: 10
    });
    req.flush({ success: true, message: 'Gift completed.' });
  });
});
