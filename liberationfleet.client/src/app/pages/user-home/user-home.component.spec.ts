import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { UserHomeComponent } from './user-home.component';
import { NotificationService } from '../../services/notification.service';
import { emptyAreaCounts } from '../../utils/notification-area.util';
import { ContentBadgeComponent } from '../../components/content-badge/content-badge.component';

@Component({
  selector: 'app-nav-layout',
  standalone: true,
  template: '<ng-content></ng-content>'
})
class StubNavLayoutComponent {
  @Input() activeTab = 'crew';
}

describe('UserHomeComponent', () => {
  let fixture: ComponentFixture<UserHomeComponent>;
  let component: UserHomeComponent;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserHomeComponent],
      providers: [
        provideRouter([]),
        {
          provide: NotificationService,
          useValue: {
            refreshBadges: jasmine.createSpy('refreshBadges'),
            areaCounts$: of(emptyAreaCounts())
          }
        }
      ]
    })
      .overrideComponent(UserHomeComponent, {
        set: {
          imports: [CommonModule, StubNavLayoutComponent, ContentBadgeComponent]
        }
      })
      .compileComponents();

    fixture = TestBed.createComponent(UserHomeComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    fixture.detectChanges();
  });

  it('should create with profile nav tab and menu links', () => {
    expect(component).toBeTruthy();

    const links = fixture.nativeElement.querySelectorAll('.menu-link');
    expect(links.length).toBe(6);
    expect(links[0].textContent).toContain('User Profile');
    expect(links[1].textContent).toContain('Gift History');
    expect(links[2].textContent).toContain('Activity center');
    expect(links[3].textContent).toContain('Preferences');
    expect(links[4].textContent).toContain('My Invitations');
    expect(links[5].textContent).toContain('Donate');
  });

  it('should navigate to invitations', () => {
    component.goToInvitations();
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/invitations']);
  });

  it('should navigate to user profile page', () => {
    component.goToUserProfile();
    expect(router.navigate).toHaveBeenCalledWith(['/app/profile/user']);
  });

  it('should navigate to gift history', () => {
    component.goToGiftHistory();
    expect(router.navigate).toHaveBeenCalledWith(['/app/profile/gift-history']);
  });

  it('should navigate to activity center placeholder', () => {
    component.goToActivityCenter();
    expect(router.navigate).toHaveBeenCalledWith(['/app/profile/activity']);
  });

  it('should navigate to preferences placeholder', () => {
    component.goToPreferences();
    expect(router.navigate).toHaveBeenCalledWith(['/app/profile/preferences']);
  });

  it('should navigate to donate', () => {
    component.goToDonate();
    expect(router.navigate).toHaveBeenCalledWith(['/app/donate']);
  });
});
