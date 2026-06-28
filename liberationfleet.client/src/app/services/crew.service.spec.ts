import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CrewService } from './crew.service';

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
});
