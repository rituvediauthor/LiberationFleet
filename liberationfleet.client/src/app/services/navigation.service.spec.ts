import { TestBed } from '@angular/core/testing';
import { NavigationEnd, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { NavigationService } from './navigation.service';

describe('NavigationService', () => {
  let service: NavigationService;
  let router: { url: string; events: Subject<unknown>; navigate: jasmine.Spy };
  let events$: Subject<unknown>;

  beforeEach(() => {
    events$ = new Subject();
    router = {
      url: '/app/crew',
      events: events$,
      navigate: jasmine.createSpy('navigate').and.returnValue(Promise.resolve(true))
    };

    TestBed.configureTestingModule({
      providers: [
        NavigationService,
        { provide: Router, useValue: router }
      ]
    });

    service = TestBed.inject(NavigationService);
  });

  function navigateTo(url: string) {
    events$.next(new NavigationEnd(1, url, url));
    router.url = url;
  }

  it('navigates to fallback when previous page was not notifications', () => {
    navigateTo('/app/crew/gift-log');
    service.back(['/app/crew']);
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew']);
  });

  it('navigates to notifications when previous page was the notifications list', () => {
    navigateTo('/app/notifications');
    navigateTo('/app/crew/gift-log');
    service.back(['/app/crew']);
    expect(router.navigate).toHaveBeenCalledWith(['/app/notifications']);
  });

  it('uses fallback when notifications was earlier but not the immediate previous page', () => {
    navigateTo('/app/notifications');
    navigateTo('/app/crew/gift-log');
    navigateTo('/app/crew/gift-log/record');
    navigateTo('/app/crew/gift-log');
    service.back(['/app/crew']);
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew']);
  });

  it('does not treat notification settings as the notifications list', () => {
    navigateTo('/app/profile/preferences/notifications');
    navigateTo('/app/crew/gift-log');
    service.back(['/app/crew']);
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew']);
  });

  it('createBackButton wires onClick to back with the given fallback', () => {
    navigateTo('/app/crew/gift-log');
    const button = service.createBackButton(['/app/crew']);
    button.onClick?.();
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew']);
  });
});
