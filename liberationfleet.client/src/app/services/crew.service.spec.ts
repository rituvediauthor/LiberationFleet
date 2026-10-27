import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CrewService } from './crew.service';
import { AUTH_TOKEN_STORAGE_KEY, CREW_MEMBERSHIP_CACHE_STORAGE_KEY } from './storage/storage-keys';

function testJwt(userId: number): string {
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const payload = btoa(JSON.stringify({
    sub: String(userId),
    exp: Math.floor(Date.now() / 1000) + 3600
  }));
  return `${header}.${payload}.sig`;
}

describe('CrewService', () => {
  let service: CrewService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [CrewService]
    });

    service = TestBed.inject(CrewService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.removeItem(AUTH_TOKEN_STORAGE_KEY);
    localStorage.removeItem(CREW_MEMBERSHIP_CACHE_STORAGE_KEY);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getMembership should GET membership status', () => {
    service.getMembership().subscribe(status => {
      expect(status.hasCrew).toBeTrue();
      expect(status.crewName).toBe('Alpha Fleet');
    });

    const req = httpMock.expectOne('/api/crews/membership');
    expect(req.request.method).toBe('GET');
    req.flush({ hasCrew: true, crewId: 1, crewName: 'Alpha Fleet' });
  });

  it('getMembership should not sticky-cache errors so a retry can succeed', () => {
    let sawError = false;
    service.getMembership().subscribe({
      next: () => fail('expected error'),
      error: () => {
        sawError = true;
      }
    });

    const failed = httpMock.expectOne('/api/crews/membership');
    failed.flush({ message: 'boom' }, { status: 500, statusText: 'Server Error' });
    expect(sawError).toBeTrue();

    service.getMembership().subscribe(status => {
      expect(status.hasCrew).toBeTrue();
    });

    const retry = httpMock.expectOne('/api/crews/membership');
    retry.flush({ hasCrew: true, crewId: 1, crewName: 'Alpha Fleet' });
  });

  it('create should POST crew payload', () => {
    const payload = {
      name: 'New Crew',
      maxSize: 8,
      privacy: 'Public' as const,
      scope: 'Online' as const
    };

    service.create(payload).subscribe();

    const req = httpMock.expectOne('/api/crews');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ success: true, message: 'Crew created successfully' });
  });

  it('search should POST search criteria', () => {
    const payload = { scope: 'Online' as const, page: 1, pageSize: 10 };

    service.search(payload).subscribe();

    const req = httpMock.expectOne('/api/crews/search');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ success: true, items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0, message: 'No crews found' });
  });

  it('submitJoinRequest should POST join request payload', () => {
    service.submitJoinRequest({ crewId: 5, acceptedRuleIds: [1, 2] }).subscribe();

    const req = httpMock.expectOne('/api/crews/join-request');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ crewId: 5, acceptedRuleIds: [1, 2] });
    req.flush({ success: true, message: 'Join request submitted', proposalId: 10 });
  });

  it('getMyJoinRequests should GET pending join requests', () => {
    service.getMyJoinRequests().subscribe(response => {
      expect(response.items.length).toBe(1);
    });

    const req = httpMock.expectOne('/api/crews/join-requests/mine');
    expect(req.request.method).toBe('GET');
    req.flush({
      success: true,
      message: 'Join requests loaded.',
      items: [{ proposalId: 10, crewId: 5, crewName: 'Alpha', status: 'Pending', approveCount: 0, disapproveCount: 0, isKeyPrepared: false, createdAt: '2026-06-18T00:00:00Z' }]
    });
  });

  it('persists membership for the signed-in user and restores it after a cache clear', () => {
    localStorage.setItem(AUTH_TOKEN_STORAGE_KEY, testJwt(4));

    service.getMembership().subscribe(status => {
      expect(status.crewName).toBe('Alpha Fleet');
    });
    httpMock.expectOne('/api/crews/membership').flush({
      hasCrew: true,
      crewId: 1,
      crewName: 'Alpha Fleet'
    });

    expect(service.peekPersistedMembership()?.crewName).toBe('Alpha Fleet');

    service.clearMembershipCache();
    expect(service.peekPersistedMembership()?.crewName).toBe('Alpha Fleet');

    service.clearSessionCache();
    expect(service.peekPersistedMembership()).toBeNull();
  });

  it('does not return another user\'s persisted membership', () => {
    localStorage.setItem(AUTH_TOKEN_STORAGE_KEY, testJwt(1));
    service.setCachedMembership({ hasCrew: true, crewId: 1, crewName: 'Alpha Fleet' });
    localStorage.setItem(AUTH_TOKEN_STORAGE_KEY, testJwt(2));
    expect(service.peekPersistedMembership()).toBeNull();
  });
});
