import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { NotificationService } from './notification.service';
import { AppIconBadgeService } from './app-icon-badge.service';

describe('NotificationService', () => {
  let service: NotificationService;
  let httpMock: HttpTestingController;
  let iconBadge: jasmine.SpyObj<AppIconBadgeService>;

  beforeEach(() => {
    iconBadge = jasmine.createSpyObj<AppIconBadgeService>('AppIconBadgeService', ['setCount']);
    iconBadge.setCount.and.returnValue(Promise.resolve());

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        NotificationService,
        { provide: AppIconBadgeService, useValue: iconBadge }
      ]
    });

    service = TestBed.inject(NotificationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('writes the home-screen badge when mark-all-read returns zero', () => {
    service.markAllRead().subscribe();

    const req = httpMock.expectOne('/api/notifications/read-all');
    expect(req.request.method).toBe('POST');
    req.flush({ success: true, unreadCount: 0, message: 'ok' });

    expect(iconBadge.setCount).toHaveBeenCalledWith(0, { force: true });
  });

  it('writes the remaining unread count after marking one notification read', () => {
    service.markRead(42).subscribe();

    const req = httpMock.expectOne('/api/notifications/42/read');
    req.flush({ success: true, unreadCount: 3, message: 'ok' });

    expect(iconBadge.setCount).toHaveBeenCalledWith(3, { force: false });
  });

  it('clears the home-screen badge on logout cache wipe', () => {
    service.clearSessionCache();
    expect(iconBadge.setCount).toHaveBeenCalledWith(0, { force: false });
  });

  it('rewrites a zero icon badge even when in-app unread is already zero', () => {
    service.syncAppIconBadge(true);
    expect(iconBadge.setCount).toHaveBeenCalledWith(0, { force: true });
  });
});
