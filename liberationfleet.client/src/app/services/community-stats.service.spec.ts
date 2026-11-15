import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CommunityActivityStats, CommunityStatsService } from './community-stats.service';

describe('CommunityStatsService', () => {
  let service: CommunityStatsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule]
    });
    service = TestBed.inject(CommunityStatsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loads community activity stats', () => {
    let result: CommunityActivityStats | undefined;
    service.getCommunityActivity().subscribe(stats => (result = stats));

    const req = httpMock.expectOne('/api/stats/community-activity');
    expect(req.request.method).toBe('GET');
    req.flush({ activeCrewCount: 3, activeCrewmateCount: 12, activeWindowDays: 30 });

    expect(result).toEqual({ activeCrewCount: 3, activeCrewmateCount: 12, activeWindowDays: 30 });
  });

  it('falls back to zeros when the request fails', () => {
    let result: CommunityActivityStats | undefined;
    service.getCommunityActivity().subscribe(stats => (result = stats));

    const req = httpMock.expectOne('/api/stats/community-activity');
    req.flush('boom', { status: 500, statusText: 'Server Error' });

    expect(result).toEqual({
      activeCrewCount: 0,
      activeCrewmateCount: 0,
      activeWindowDays: 30
    });
  });
});
