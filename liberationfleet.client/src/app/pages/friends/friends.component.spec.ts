import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { FriendsComponent } from './friends.component';
import { FriendService } from '../../services/friend.service';
import { CrewmateService } from '../../services/crewmate.service';
import { NotificationContentService } from '../../services/notification-content.service';
import { ToastService } from '../../components/toast/toast.component';
import { provideAppShellMocks } from '../../testing/test-helpers';

describe('FriendsComponent', () => {
  let fixture: ComponentFixture<FriendsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FriendsComponent],
      providers: [
        provideRouter([]),
        ...provideAppShellMocks(),
        {
          provide: FriendService,
          useValue: { getFriends: () => of({ success: true, message: '', items: [] }) }
        },
        { provide: CrewmateService, useValue: {} },
        { provide: NotificationContentService, useValue: { markVisited: () => undefined } },
        { provide: ToastService, useValue: { success: () => undefined, error: () => undefined } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FriendsComponent);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
