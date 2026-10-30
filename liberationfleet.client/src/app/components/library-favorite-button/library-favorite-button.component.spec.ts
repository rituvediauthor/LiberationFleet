import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject } from 'rxjs';
import { LibraryFavoriteButtonComponent } from './library-favorite-button.component';
import { LibraryService } from '../../services/library.service';
import { ToastService } from '../toast/toast.component';

describe('LibraryFavoriteButtonComponent', () => {
  let fixture: ComponentFixture<LibraryFavoriteButtonComponent>;
  let favoriteIds: Set<number>;
  let favoriteIdsChanged$: BehaviorSubject<ReadonlySet<number>>;

  beforeEach(async () => {
    favoriteIds = new Set<number>([11]);
    favoriteIdsChanged$ = new BehaviorSubject<ReadonlySet<number>>(favoriteIds);
    const library = {
      favoriteIdsChanged$,
      ensureFavoriteIdsLoaded: () => undefined,
      isFavorited: (unitId: number) => favoriteIds.has(unitId)
    };

    await TestBed.configureTestingModule({
      imports: [LibraryFavoriteButtonComponent],
      providers: [
        { provide: LibraryService, useValue: library },
        { provide: ToastService, useValue: { success: () => undefined, error: () => undefined } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LibraryFavoriteButtonComponent);
    fixture.componentRef.setInput('unitId', 11);
    fixture.detectChanges();
  });

  it('shows the saved state for the current offering and updates when the carousel moves on', () => {
    expect(fixture.nativeElement.querySelector('button')?.classList.contains('active')).toBeTrue();

    fixture.componentRef.setInput('unitId', 12);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button')?.classList.contains('active')).toBeFalse();

    fixture.componentRef.setInput('unitId', 11);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button')?.classList.contains('active')).toBeTrue();
  });
});
