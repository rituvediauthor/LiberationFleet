import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CreateCrewComponent } from './create-crew.component';
import { CrewService } from '../../services/crew.service';
import { ToastService } from '../../components/toast/toast.component';
import { createCrewServiceMock, createToastServiceMock } from '../../testing/test-helpers';

describe('CreateCrewComponent', () => {
  let fixture: ComponentFixture<CreateCrewComponent>;
  let component: CreateCrewComponent;
  let crewService: jasmine.SpyObj<CrewService>;
  let toastService: jasmine.SpyObj<ToastService>;
  let router: Router;

  beforeEach(async () => {
    crewService = createCrewServiceMock();
    toastService = createToastServiceMock();

    await TestBed.configureTestingModule({
      imports: [CreateCrewComponent],
      providers: [
        provideRouter([]),
        { provide: CrewService, useValue: crewService },
        { provide: ToastService, useValue: toastService }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(CreateCrewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create with invalid form and disabled create button', () => {
    expect(component.form.invalid).toBeTrue();
    expect(component.createButton.disabled).toBeTrue();
  });

  it('should not submit when form is invalid', () => {
    component.onSubmit();
    expect(crewService.create).not.toHaveBeenCalled();
  });

  it('should create crew and navigate on success', () => {
    crewService.create.and.returnValue(of({ success: true, message: 'Crew created successfully' }));

    component.form.setValue({
      name: 'My Crew',
      maxSize: 6,
      privacy: 'Public',
      scope: 'Online',
      zipCode: '',
      radiusMiles: 25
    });
    component.onSubmit();

    expect(crewService.create).toHaveBeenCalledWith({
      name: 'My Crew',
      maxSize: 6,
      privacy: 'Public',
      scope: 'Online',
      zipCode: undefined,
      radiusMiles: undefined
    });
    expect(toastService.success).toHaveBeenCalledWith('Crew created successfully');
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew']);
  });

  it('should include zip and radius for local crews', () => {
    crewService.create.and.returnValue(of({ success: true, message: 'Crew created successfully' }));

    component.form.setValue({
      name: 'Local Crew',
      maxSize: 6,
      privacy: 'Public',
      scope: 'Local',
      zipCode: '90210',
      radiusMiles: 30
    });
    component.onSubmit();

    expect(crewService.create).toHaveBeenCalledWith(jasmine.objectContaining({
      scope: 'Local',
      zipCode: '90210',
      radiusMiles: 30
    }));
  });

  it('should show error toast on failure response', () => {
    crewService.create.and.returnValue(of({ success: false, message: 'Already in a crew' }));

    component.form.setValue({
      name: 'My Crew',
      maxSize: 6,
      privacy: 'Public',
      scope: 'Online',
      zipCode: '',
      radiusMiles: 25
    });
    component.onSubmit();

    expect(toastService.error).toHaveBeenCalledWith('Already in a crew');
    expect(component.isLoading).toBeFalse();
  });

  it('should show error toast on HTTP error', () => {
    crewService.create.and.returnValue(throwError(() => ({ error: { message: 'Server error' } })));

    component.form.setValue({
      name: 'My Crew',
      maxSize: 6,
      privacy: 'Public',
      scope: 'Online',
      zipCode: '',
      radiusMiles: 25
    });
    component.onSubmit();

    expect(toastService.error).toHaveBeenCalledWith('Server error');
  });

  it('should navigate back to crew home', () => {
    component.backButton.onClick?.();
    expect(router.navigate).toHaveBeenCalledWith(['/app/crew']);
  });
});
