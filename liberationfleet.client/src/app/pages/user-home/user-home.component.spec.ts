import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { UserHomeComponent } from './user-home.component';

describe('UserHomeComponent', () => {
  let fixture: ComponentFixture<UserHomeComponent>;
  let component: UserHomeComponent;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserHomeComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(UserHomeComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    fixture.detectChanges();
  });

  it('should create with profile nav tab and menu links', () => {
    expect(component).toBeTruthy();

    const navLayout = fixture.nativeElement.querySelector('app-nav-layout');
    expect(navLayout).toBeTruthy();

    const links = fixture.nativeElement.querySelectorAll('.menu-link');
    expect(links.length).toBe(3);
    expect(links[0].textContent).toContain('User Profile');
    expect(links[1].textContent).toContain('Activity center');
    expect(links[2].textContent).toContain('Preferences');
  });

  it('should navigate to user profile page', () => {
    component.goToUserProfile();
    expect(router.navigate).toHaveBeenCalledWith(['/app/profile/user']);
  });

  it('should navigate to activity center placeholder', () => {
    component.goToActivityCenter();
    expect(router.navigate).toHaveBeenCalledWith(['/app/profile/activity']);
  });

  it('should navigate to preferences placeholder', () => {
    component.goToPreferences();
    expect(router.navigate).toHaveBeenCalledWith(['/app/profile/preferences']);
  });
});
