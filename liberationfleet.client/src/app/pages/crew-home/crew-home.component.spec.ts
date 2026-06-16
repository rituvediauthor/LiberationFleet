import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
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
    giftService.getNextAidInfo.and.returnValue({ recipientName: 'Ritu', amount: 20 });

    await TestBed.configureTestingModule({
      imports: [CrewHomeComponent],
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
    expect(component.membership?.hasCrew).toBeFalse();
  });

  it('should show welcome actions when user has no crew', () => {
    const buttons = fixture.nativeElement.querySelectorAll('.action-btn');
    expect(buttons.length).toBe(2);
    expect(buttons[0].textContent).toContain('Create Crew');
    expect(buttons[1].textContent).toContain('Join Crew');
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

    expect(fixture.nativeElement.querySelector('.join-code')?.textContent).toContain('ALPHA123');
    expect(fixture.nativeElement.querySelector('.crew-name')?.textContent).toContain('Alpha Fleet');
    expect(fixture.nativeElement.querySelector('.info-text')?.textContent).toContain('Ritu needs $20');
    expect(fixture.nativeElement.querySelectorAll('.menu-link').length).toBe(4);
  });

  it('should navigate to gift log', () => {
    component.goToGiftLog();
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/gift-log']);
  });

  it('should navigate to create crew page', () => {
    component.goToCreateCrew();
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/create']);
  });

  it('should navigate to join crew page', () => {
    component.goToJoinCrew();
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/join']);
  });
});
