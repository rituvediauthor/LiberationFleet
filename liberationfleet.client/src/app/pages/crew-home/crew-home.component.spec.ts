import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CrewHomeComponent } from './crew-home.component';
import { CrewService } from '../../services/crew.service';
import { createCrewServiceMock } from '../../testing/test-helpers';

describe('CrewHomeComponent', () => {
  let fixture: ComponentFixture<CrewHomeComponent>;
  let component: CrewHomeComponent;
  let crewService: jasmine.SpyObj<CrewService>;
  let router: Router;

  beforeEach(async () => {
    crewService = createCrewServiceMock();
    crewService.getMembership.and.returnValue(of({ hasCrew: false }));

    await TestBed.configureTestingModule({
      imports: [CrewHomeComponent],
      providers: [
        provideRouter([]),
        { provide: CrewService, useValue: crewService }
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

  it('should show crew name when user has a crew', () => {
    crewService.getMembership.and.returnValue(of({ hasCrew: true, crewId: 1, crewName: 'Alpha Fleet' }));
    component.ngOnInit();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain('Alpha Fleet');
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
