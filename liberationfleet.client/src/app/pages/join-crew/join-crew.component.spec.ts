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

  it('should create in find mode with disabled join button', () => {
    expect(component.isFindMode).toBeTrue();
    expect(component.joinButton.disabled).toBeTrue();
  });

  it('should search online crews when in find mode', fakeAsync(() => {
    crewService.search.and.returnValue(of({
      success: true,
      message: 'Crews found',
      items: [{ id: 1, name: 'Alpha', maxSize: 10, memberCount: 3, privacy: 'Public', scope: 'Online', joinCode: 'ABC12345' }],
      page: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1
    }));

    component.form.patchValue({ mode: 'find', scope: 'Online' });
    tick(400);

    expect(crewService.search).toHaveBeenCalledWith(jasmine.objectContaining({ scope: 'Online', page: 1, pageSize: 10 }));
    expect(component.searchResults.length).toBe(1);
  }));

  it('should enable join button when crew is selected', () => {
    component.selectCrew({ id: 1, name: 'Alpha', maxSize: 10, memberCount: 3, privacy: 'Public', scope: 'Online', joinCode: 'ABC12345' });
    expect(component.joinButton.disabled).toBeFalse();
  });

  it('should join by crew id in find mode', () => {
    crewService.join.and.returnValue(of({ success: true, message: 'Joined crew successfully' }));
    component.selectCrew({ id: 5, name: 'Alpha', maxSize: 10, memberCount: 3, privacy: 'Public', scope: 'Online', joinCode: 'ABC12345' });

    component.onJoin();

    expect(crewService.join).toHaveBeenCalledWith({ crewId: 5 });
    expect(toastService.success).toHaveBeenCalledWith('Joined crew successfully');
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew']);
  });

  it('should join by code in code mode', fakeAsync(() => {
    crewService.join.and.returnValue(of({ success: true, message: 'Joined crew successfully' }));
    component.form.patchValue({ mode: 'code', joinCode: 'join1234' });
    tick(400);

    component.onJoin();

    expect(crewService.join).toHaveBeenCalledWith({ joinCode: 'JOIN1234' });
  }));

  it('should show error toast on join failure', fakeAsync(() => {
    crewService.join.and.returnValue(of({ success: false, message: 'Crew is full' }));
    component.form.patchValue({ mode: 'code', joinCode: 'JOIN1234' });
    tick(400);

    component.onJoin();

    expect(toastService.error).toHaveBeenCalledWith('Crew is full');
    expect(component.isJoining).toBeFalse();
  }));

  it('should show error toast on search failure', () => {
    crewService.search.and.returnValue(throwError(() => ({ error: { message: 'Search failed' } })));

    component.runSearch(1);

    expect(toastService.error).toHaveBeenCalledWith('Search failed');
    expect(component.isSearching).toBeFalse();
  });

  it('should navigate back to crew home', () => {
    component.backButton.onClick?.();
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew']);
  });
});
