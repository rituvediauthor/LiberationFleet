import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ProfileService } from './profile.service';

describe('ProfileService', () => {
  let service: ProfileService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [ProfileService]
    });

    service = TestBed.inject(ProfileService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should GET profile from API', () => {
    service.getProfile().subscribe(profile => {
      expect(profile.username).toBe('James');
    });

    const req = httpMock.expectOne('/api/profile');
    expect(req.request.method).toBe('GET');
    req.flush({
      id: 4,
      username: 'James',
      email: 'james@example.com',
      paymentPlatforms: [],
      inNeedOfAid: true,
      emergencyLevel: 0,
      needsSurvivalAid: false,
      stats: {
        sacrificeCount: 0,
        averageMonthlyContributions: 0,
        membershipStatus: false,
        lifetimeContributions: 0,
        receptionLastYear: 0,
        percentBoost: 0,
        priorityScore: 0
      }
    });
  });

  it('should PUT profile updates to API', () => {
    const payload = {
      username: 'James',
      email: 'james@example.com',
      paymentPlatforms: [],
      inNeedOfAid: true,
      emergencyLevel: 1,
      needsSurvivalAid: false
    };

    service.updateProfile(payload).subscribe();

    const req = httpMock.expectOne('/api/profile');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(payload);
    req.flush({ success: true, message: 'Profile updated successfully' });
  });

  it('should assign temp payment platform ids that fit in a 32-bit integer', () => {
    const first = service.createPaymentPlatformAccount();
    const second = service.createPaymentPlatformAccount();

    expect(first.id).toBe(-1);
    expect(second.id).toBe(-2);
    expect(first.id).toBeGreaterThan(-2_147_483_648);
    expect(first.id).toBeLessThan(2_147_483_647);
  });
});
