import { IdlePreloadStrategy } from './idle-preload.strategy';
import { fakeAsync, tick } from '@angular/core/testing';
import { of } from 'rxjs';

describe('IdlePreloadStrategy', () => {
  const strategy = new IdlePreloadStrategy();

  it('preloads tab-root paths after a short delay', fakeAsync(() => {
    let loaded = false;
    strategy.preload({ path: 'app/crew' }, () => {
      loaded = true;
      return of(null);
    }).subscribe();

    expect(loaded).toBeFalse();
    tick(1_200);
    expect(loaded).toBeTrue();
  }));

  it('does not preload deep library or chat screens', fakeAsync(() => {
    let loaded = false;
    strategy.preload({ path: 'app/crew/library-of-things/tasks' }, () => {
      loaded = true;
      return of(null);
    }).subscribe();

    tick(5_000);
    expect(loaded).toBeFalse();
  }));
});
