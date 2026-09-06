import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';
import { AppComponent } from './app.component';
import { APP_ENVIRONMENT } from './config/app-environment';
import { AuthService } from './services/auth.service';
import { CrewService } from './services/crew.service';
import { CryptoSessionService } from './services/crypto/crypto-session.service';
import { CrewCryptoSyncService } from './services/crew-crypto-sync.service';
import { FleetCryptoSyncService } from './services/fleet-crypto-sync.service';
import { NotificationHubService } from './services/notification-hub.service';
import { NotificationService } from './services/notification.service';
import { createAuthServiceMock, createCrewServiceMock } from './testing/test-helpers';

describe('AppComponent', () => {
  let fixture: ComponentFixture<AppComponent>;

  beforeEach(async () => {
    const auth = createAuthServiceMock();
    auth.getEncryptionReady = jasmine.createSpy('getEncryptionReady').and.returnValue(Promise.resolve());
    auth.isAuthenticated.and.returnValue(false);
    auth.getToken.and.returnValue(null);
    auth.needsEncryptionUnlock = jasmine.createSpy('needsEncryptionUnlock').and.returnValue(false);

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: APP_ENVIRONMENT, useValue: { production: false, apiBaseUrl: '' } },
        { provide: AuthService, useValue: auth },
        { provide: CrewService, useValue: createCrewServiceMock() },
        {
          provide: CryptoSessionService,
          useValue: { unlocked$: of(false), isUnlocked: () => false }
        },
        {
          provide: CrewCryptoSyncService,
          useValue: { syncActiveCrewKeyDistributions: jasmine.createSpy('syncCrew') }
        },
        {
          provide: FleetCryptoSyncService,
          useValue: { syncActiveFleetKeyDistributions: jasmine.createSpy('syncFleet') }
        },
        {
          provide: NotificationHubService,
          useValue: {
            notificationReceived$: new Subject(),
            connect: jasmine.createSpy('connect').and.returnValue(Promise.resolve())
          }
        },
        {
          provide: NotificationService,
          useValue: { refreshBadges: jasmine.createSpy('refreshBadges') }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render router outlet and toast container', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('router-outlet')).toBeTruthy();
    expect(element.querySelector('app-toast-container')).toBeTruthy();
  });
});
