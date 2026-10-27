import { fakeAsync, flush, tick } from '@angular/core/testing';
import { Capacitor } from '@capacitor/core';
import { NativeSplashService } from './native-splash.service';

function isHidden(service: NativeSplashService): boolean {
  return (service as unknown as { hidden: boolean }).hidden;
}

describe('NativeSplashService', () => {
  let service: NativeSplashService;
  let nativeSpy: jasmine.Spy;

  beforeEach(() => {
    service = new NativeSplashService();
    nativeSpy = spyOn(Capacitor, 'isNativePlatform').and.returnValue(true);
  });

  it('does not hide immediately when the fallback timer starts', fakeAsync(() => {
    service.startFallbackTimer();
    tick(0);
    expect(isHidden(service)).toBeFalse();
    nativeSpy.and.returnValue(false);
    tick(6_000);
    flush();
    expect(isHidden(service)).toBeTrue();
  }));

  it('hides after the fallback timeout if first paint never arrives', fakeAsync(() => {
    service.startFallbackTimer();
    tick(5_999);
    expect(isHidden(service)).toBeFalse();
    nativeSpy.and.returnValue(false);
    tick(1);
    flush();
    expect(isHidden(service)).toBeTrue();
  }));

  it('hides once after two animation frames', fakeAsync(() => {
    nativeSpy.and.returnValue(false);
    const frames: FrameRequestCallback[] = [];
    spyOn(window, 'requestAnimationFrame').and.callFake((cb: FrameRequestCallback) => {
      frames.push(cb);
      return frames.length;
    });

    service.hideAfterFirstPaint();
    expect(isHidden(service)).toBeFalse();
    frames[0](0);
    expect(isHidden(service)).toBeFalse();
    frames[1](0);
    flush();
    expect(isHidden(service)).toBeTrue();

    service.hideAfterFirstPaint();
    expect(isHidden(service)).toBeTrue();
  }));
});
