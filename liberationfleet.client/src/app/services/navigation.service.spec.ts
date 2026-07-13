import { TestBed } from '@angular/core/testing';
import { Location } from '@angular/common';
import { NavigationEnd, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { NavigationService } from './navigation.service';

describe('NavigationService', () => {
  let service: NavigationService;
  let location: jasmine.SpyObj<Location>;
  let router: { url: string; events: Subject<unknown>; navigate: jasmine.Spy };
  let events$: Subject<unknown>;

  beforeEach(() => {
    events$ = new Subject();
    location = jasmine.createSpyObj('Location', ['back']);
    router = {
      url: '/app/crew/gift-log',
      events: events$,
      navigate: jasmine.createSpy('navigate').and.returnValue(Promise.resolve(true))
    };

    TestBed.configureTestingModule({
      providers: [
        NavigationService,
        { provide: Location, useValue: location },
        { provide: Router, useValue: router }
      ]
    });

    service = TestBed.inject(NavigationService);
  });

  afterEach(() => {
    try {
      jasmine.clock().uninstall();
    } catch {
      // clock may not be installed
    }
  });

  it('uses fallback when history back is a no-op', () => {
    jasmine.clock().install();
    service.back(['/app/crew']);
    expect(location.back).toHaveBeenCalled();

    jasmine.clock().tick(50);
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew'], { replaceUrl: true });
  });

  it('uses fallback when back lands on a season redirect trap', () => {
    service.back(['/app/crew']);
    events$.next(new NavigationEnd(1, '/app/crew/season-setup', '/app/crew/season-setup'));

    expect(router.navigate).toHaveBeenCalledWith(['/app/crew'], { replaceUrl: true });
  });

  it('uses fallback when back lands on join-season redirect trap', () => {
    service.back(['/app/crew']);
    events$.next(new NavigationEnd(1, '/app/crew/join-season', '/app/crew/join-season'));

    expect(router.navigate).toHaveBeenCalledWith(['/app/crew'], { replaceUrl: true });
  });

  it('does not force fallback when back reaches a stable non-trap page', () => {
    jasmine.clock().install();
    service.back(['/app/crew']);
    events$.next(new NavigationEnd(1, '/app/crew', '/app/crew'));
    router.url = '/app/crew';

    jasmine.clock().tick(50);
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
