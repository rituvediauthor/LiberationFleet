import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Capacitor } from '@capacitor/core';
import {
  GOOGLE_PLAY_STORE_URL,
  PlayStoreBannerComponent
} from './play-store-banner.component';

describe('PlayStoreBannerComponent', () => {
  let fixture: ComponentFixture<PlayStoreBannerComponent>;
  let component: PlayStoreBannerComponent;
  let originalUserAgent: string;

  beforeEach(async () => {
    localStorage.removeItem('lf-play-store-banner-dismissed');
    originalUserAgent = navigator.userAgent;
    await TestBed.configureTestingModule({
      imports: [PlayStoreBannerComponent]
    }).compileComponents();
  });

  afterEach(() => {
    localStorage.removeItem('lf-play-store-banner-dismissed');
    Object.defineProperty(navigator, 'userAgent', {
      configurable: true,
      get: () => originalUserAgent
    });
  });

  function setAndroidBrowser(): void {
    spyOn(Capacitor, 'isNativePlatform').and.returnValue(false);
    Object.defineProperty(navigator, 'userAgent', {
      configurable: true,
      get: () => 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/120.0.0.0 Mobile Safari/537.36'
    });
  }

  function setDesktopBrowser(): void {
    spyOn(Capacitor, 'isNativePlatform').and.returnValue(false);
    Object.defineProperty(navigator, 'userAgent', {
      configurable: true,
      get: () => 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36'
    });
  }

  it('is hidden when not an Android mobile browser', () => {
    setDesktopBrowser();
    fixture = TestBed.createComponent(PlayStoreBannerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    expect(component.visible).toBeFalse();
    expect(fixture.nativeElement.querySelector('.play-store-banner')).toBeNull();
  });

  it('shows on Android browsers and links to the Play Store listing', () => {
    setAndroidBrowser();
    fixture = TestBed.createComponent(PlayStoreBannerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.visible).toBeTrue();
    const link = fixture.nativeElement.querySelector('.play-store-link') as HTMLAnchorElement;
    expect(link).toBeTruthy();
    expect(link.getAttribute('href')).toBe(GOOGLE_PLAY_STORE_URL);
  });

  it('dismisses and stays hidden after reload of the component', () => {
    setAndroidBrowser();
    fixture = TestBed.createComponent(PlayStoreBannerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    fixture.nativeElement.querySelector('.dismiss-btn')?.click();
    fixture.detectChanges();
    expect(component.visible).toBeFalse();
    expect(localStorage.getItem('lf-play-store-banner-dismissed')).toBe('1');

    const again = TestBed.createComponent(PlayStoreBannerComponent);
    again.detectChanges();
    expect(again.componentInstance.visible).toBeFalse();
  });
});
