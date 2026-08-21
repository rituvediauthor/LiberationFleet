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
    service.getLogs().subscribe(page => {
      expect(page.items.length).toBe(1);
      expect(page.hasMore).toBeFalse();
      expect(page.items[0].message).toBe('James gave $30 to Ritu via PayPal');
      expect(page.items[0].timestamp instanceof Date).toBeTrue();
    });

    const req = httpMock.expectOne(r => r.url.startsWith('/api/gifts/log') && r.params.get('limit') === '50');
    expect(req.request.method).toBe('GET');
    req.flush({
      success: true,
      message: 'Gift log loaded.',
      hasMore: false,
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

  it('should request older gift log pages with cursor params', () => {
    service.getLogs({
      limit: 50,
      beforeCreatedAt: '2026-06-01T00:00:00.000Z',
      beforeId: 42
    }).subscribe(page => {
      expect(page.items.length).toBe(0);
      expect(page.hasMore).toBeTrue();
    });

    const req = httpMock.expectOne(r =>
      r.url.startsWith('/api/gifts/log')
      && r.params.get('limit') === '50'
      && r.params.get('beforeCreatedAt') === '2026-06-01T00:00:00.000Z'
      && r.params.get('beforeId') === '42');
    req.flush({ success: true, message: 'Gift log loaded.', hasMore: true, items: [] });
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

  it('should load gift detail from the API', () => {
    service.getGiftDetail(7).subscribe(detail => {
      expect(detail.id).toBe(7);
      expect(detail.comments.length).toBe(1);
      expect(detail.comments[0].createdAt instanceof Date).toBeTrue();
    });

    const req = httpMock.expectOne('/api/gifts/log/7');
    expect(req.request.method).toBe('GET');
    req.flush({
      success: true,
      message: 'Gift loaded.',
      entry: {
        id: 7,
        type: 'direct',
        giverId: 1,
        giverName: 'James',
        recipientId: 2,
        recipientName: 'Ritu',
        amount: 30,
        platform: 'PayPal',
        timestamp: '2026-06-14T12:00:00Z',
        message: 'James gave $30 to Ritu via PayPal',
        relatedUserIds: [1, 2],
        likeCount: 2,
        likedByCurrentUser: true,
        commentCount: 1,
        comments: [{
          id: 11,
          authorUserId: 2,
          authorUsername: 'Ritu',
          createdAt: '2026-06-15T12:00:00Z',
          replyCount: 0,
          likeCount: 0,
          likedByCurrentUser: false
        }]
      }
    });
  });

  it('should toggle gift like through the API', () => {
    service.toggleGiftLike(7).subscribe(response => {
      expect(response.success).toBeTrue();
      expect(response.liked).toBeTrue();
      expect(response.likeCount).toBe(3);
    });

    const req = httpMock.expectOne('/api/gifts/log/7/like');
    expect(req.request.method).toBe('POST');
    req.flush({ success: true, message: 'Like updated.', liked: true, likeCount: 3 });
  });

  it('should load gift likers from the API', () => {
    service.getGiftLikers(7).subscribe(items => {
      expect(items.length).toBe(1);
      expect(items[0].username).toBe('Ritu');
    });

    const req = httpMock.expectOne('/api/gifts/log/7/likers');
    req.flush({ success: true, message: 'Likers loaded.', items: [{ userId: 2, username: 'Ritu' }] });
  });

  it('should load season profile from the API', () => {
    service.getSeasonProfile().subscribe(profile => {
      expect(profile.estimatedMonthlyContribution).toBe(25);
      expect(profile.identityGroups).toEqual(['Woman']);
    });

    const req = httpMock.expectOne('/api/gifts/season-profile');
    req.flush({
      success: true,
      message: 'Season profile loaded.',
      profile: {
        paymentPlatforms: [],
        inNeedOfAid: true,
        emergencyLevel: 0,
        peopleRepresentedCount: 1,
        disabilityLevel: 0,
        identityGroups: ['Woman'],
        needsSurvivalAid: false,
        canToggleInNeedOff: false,
        inNeedToggleThreshold: 0,
        estimatedMonthlyContribution: 25,
        canEditEstimatedContribution: true,
        priorityScore: 10
      }
    });
  });

  it('should update season profile through the API', () => {
    service.updateSeasonProfile({
      paymentPlatforms: [],
      inNeedOfAid: true,
      emergencyLevel: 1,
      peopleRepresentedCount: 2,
      disabilityLevel: 0,
      identityGroups: ['Woman'],
      needsSurvivalAid: false,
      estimatedMonthlyContribution: 30
    }).subscribe(response => {
      expect(response.success).toBeTrue();
    });

    const req = httpMock.expectOne('/api/gifts/season-profile');
    expect(req.request.method).toBe('PUT');
    req.flush({ success: true, message: 'Season profile saved.', profile: { paymentPlatforms: [], inNeedOfAid: true, emergencyLevel: 1, peopleRepresentedCount: 2, disabilityLevel: 0, identityGroups: ['Woman'], needsSurvivalAid: false, canToggleInNeedOff: false, inNeedToggleThreshold: 0, estimatedMonthlyContribution: 30, canEditEstimatedContribution: true, priorityScore: 10 } });
  });
});
