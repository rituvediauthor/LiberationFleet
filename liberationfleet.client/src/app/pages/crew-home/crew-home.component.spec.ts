import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { CrewHomeComponent } from './crew-home.component';
import { CrewService } from '../../services/crew.service';
import { GiftService } from '../../services/gift.service';
import { createCrewServiceMock, createGiftServiceMock } from '../../testing/test-helpers';

describe('CrewHomeComponent', () => {
  let fixture: ComponentFixture<CrewHomeComponent>;
  let component: CrewHomeComponent;
  let crewService: jasmine.SpyObj<CrewService>;
  let giftService: jasmine.SpyObj<GiftService>;
  let router: Router;

  beforeEach(async () => {
    crewService = createCrewServiceMock();
    giftService = createGiftServiceMock();
    crewService.getMembership.and.returnValue(of({ hasCrew: false }));
    giftService.getSeasonStatus.and.returnValue(of({
      seasonStarted: true,
      userInSeason: true,
      userSeasonReady: true,
      readyCount: 3,
      canStartSeason: false
    }));
    giftService.getNextAidInfo.and.returnValue(of({
      recipientName: 'Ritu',
      amount: 20,
      platformDisplayKind: 'preferred',
      platformName: 'Venmo',
      platformHandle: '@ritu'
    }));

    await TestBed.configureTestingModule({
      imports: [CrewHomeComponent, HttpClientTestingModule],
      providers: [
        provideRouter([]),
        { provide: CrewService, useValue: crewService },
        { provide: GiftService, useValue: giftService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(CrewHomeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load membership on init', () => {
    expect(component).toBeTruthy();
    expect(crewService.getMembership).toHaveBeenCalled();
    expect(component.loading).toBeFalse();
    expect(component.membership?.hasCrew).toBeFalse();
  });

  it('should show welcome actions when user has no crew', () => {
    const buttons = fixture.nativeElement.querySelectorAll('.action-btn');
    expect(buttons.length).toBe(3);
    expect(buttons[0].textContent).toContain('Create Crew');
    expect(buttons[1].textContent).toContain('Join Crew');
    expect(buttons[2].textContent).toContain('My Join Requests');
  });

  it('should show crew dashboard when user has a crew', () => {
    crewService.getMembership.and.returnValue(of({
      hasCrew: true,
      crewId: 1,
      crewName: 'Alpha Fleet',
      joinCode: 'ALPHA123'
    }));
    component.ngOnInit();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.crew-name-link .menu-label')?.textContent).toContain('Alpha Fleet');
    expect(fixture.nativeElement.querySelector('.info-text')?.textContent).toContain('Ritu needs $20');
    expect(fixture.nativeElement.querySelector('.info-platform')?.textContent).toContain('Venmo: @ritu');
    expect(fixture.nativeElement.querySelectorAll('.menu-link').length).toBe(9);
  });

  it('should navigate to gift log', () => {
    component.goToGiftLog();
    expect(giftService.navigateToGiftLogEntry).toHaveBeenCalledWith(router);
  });

  it('should navigate to create crew page', () => {
    component.goToCreateCrew();
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/create']);
  });

  it('should navigate to join crew page', () => {
    component.goToJoinCrew();
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/join']);
  });

  it('should route the chats menu button to the chat list page', async () => {
    crewService.getMembership.and.returnValue(of({
      hasCrew: true,
      crewId: 1,
      crewName: 'Alpha Fleet',
      joinCode: 'ALPHA123'
    }));

    const navigateByUrlSpy = spyOn(router, 'navigateByUrl').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(CrewHomeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const chatsButton = Array.from(fixture.nativeElement.querySelectorAll('.menu-link') as NodeListOf<Element>)
      .find(button => button.textContent?.includes('Chats')) as HTMLButtonElement | undefined;

    expect(chatsButton).toBeDefined();

    chatsButton?.click();
    await fixture.whenStable();

    expect(navigateByUrlSpy).toHaveBeenCalled();
  });
});
