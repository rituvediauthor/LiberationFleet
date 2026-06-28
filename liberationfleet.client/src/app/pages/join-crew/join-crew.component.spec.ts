import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { JoinCrewComponent } from './join-crew.component';
import { CrewService } from '../../services/crew.service';
import { ToastService } from '../../components/toast/toast.component';
import { createCrewServiceMock, createToastServiceMock } from '../../testing/test-helpers';

describe('JoinCrewComponent', () => {
  let fixture: ComponentFixture<JoinCrewComponent>;
  let component: JoinCrewComponent;
  let crewService: jasmine.SpyObj<CrewService>;
  let toastService: jasmine.SpyObj<ToastService>;
  let router: Router;

  beforeEach(async () => {
    crewService = createCrewServiceMock();
    toastService = createToastServiceMock();
    crewService.search.and.returnValue(of({
      success: true,
      message: 'No crews found',
      items: [],
      page: 1,
      pageSize: 10,
      totalCount: 0,
      totalPages: 0
    }));
    crewService.getPublicRules.and.returnValue(of({
      success: true,
      message: 'Public rules loaded.',
      crewId: 5,
      crewName: 'Alpha',
      items: [{ id: 1, title: 'Rule 1', description: 'Be kind' }]
    }));
    crewService.submitJoinRequest.and.returnValue(of({
      success: true,
      message: 'Join request submitted',
      proposalId: 10
    }));

    await TestBed.configureTestingModule({
      imports: [JoinCrewComponent],
      providers: [
        provideRouter([]),
        { provide: CrewService, useValue: crewService },
        { provide: ToastService, useValue: toastService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(JoinCrewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create in find mode with disabled continue button', () => {
    expect(component.isFindMode).toBeTrue();
    expect(component.primaryButton.disabled).toBeTrue();
  });

  it('should search online crews on page load', () => {
    expect(crewService.search).toHaveBeenCalledWith(jasmine.objectContaining({
      scope: 'Online',
      page: 1,
      pageSize: 10
    }));
    expect(component.hasSearched).toBeTrue();
  });

  it('should enable continue button when crew is selected', () => {
    component.selectCrew({ id: 1, name: 'Alpha', maxSize: 10, memberCount: 3, privacy: 'Public', scope: 'Online', joinCode: 'ABC12345' });
    expect(component.primaryButton.disabled).toBeFalse();
  });

  it('should load public rules when continuing in find mode', () => {
    component.selectCrew({ id: 5, name: 'Alpha', maxSize: 10, memberCount: 3, privacy: 'Public', scope: 'Online', joinCode: 'ABC12345' });
    component.onContinueToRules();

    expect(crewService.getPublicRules).toHaveBeenCalledWith(5);
    expect(component.joinStep).toBe('rules');
    expect(component.publicRules.length).toBe(1);
  });

  it('should require all rules accepted before requesting to join', () => {
    component.selectCrew({ id: 5, name: 'Alpha', maxSize: 10, memberCount: 3, privacy: 'Public', scope: 'Online', joinCode: 'ABC12345' });
    component.onContinueToRules();
    expect(component.primaryButton.disabled).toBeTrue();

    component.toggleRuleAcceptance(1, true);
    expect(component.primaryButton.disabled).toBeFalse();
  });

  it('should submit join request after accepting rules', () => {
    component.selectCrew({ id: 5, name: 'Alpha', maxSize: 10, memberCount: 3, privacy: 'Public', scope: 'Online', joinCode: 'ABC12345' });
    component.onContinueToRules();
    component.toggleRuleAcceptance(1, true);

    component.onSubmitJoinRequest();

    expect(crewService.submitJoinRequest).toHaveBeenCalledWith({
      crewId: 5,
      acceptedRuleIds: [1]
    });
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew/join-requests']);
  });

  it('should keep continue disabled until join code is 8 characters', fakeAsync(() => {
    component.form.patchValue({ mode: 'code', joinCode: 'JOIN12' });
    tick(400);
    expect(component.primaryButton.disabled).toBeTrue();

    component.form.patchValue({ joinCode: 'JOIN1234' });
    tick(400);
    expect(component.primaryButton.disabled).toBeFalse();
  }));

  it('should show error toast on search failure', () => {
    crewService.search.and.returnValue(throwError(() => ({ error: { message: 'Search failed' } })));

    component.runSearch(1);

    expect(toastService.error).toHaveBeenCalledWith('Search failed');
    expect(component.isSearching).toBeFalse();
  });

  it('should navigate back to crew home from select step', () => {
    component.backButton.onClick?.();
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew']);
  });
});
