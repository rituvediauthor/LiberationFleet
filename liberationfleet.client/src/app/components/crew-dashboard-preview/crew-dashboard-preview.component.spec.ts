import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CrewDashboardPreviewComponent } from './crew-dashboard-preview.component';
import { CrewDashboardPreview } from '../../models/crew-dashboard-preview.model';

@Component({
  selector: 'app-user-avatar',
  standalone: true,
  template: ''
})
class StubUserAvatarComponent {
  @Input() resourceId: string | null | undefined;
  @Input() crewId: number | null | undefined;
  @Input() fleetId: number | null | undefined;
  @Input() fallbackInitial = '';
  @Input() size: string | null = null;
  @Input() alt = '';
  @Input() anonymous = false;
}

@Component({
  selector: 'app-library-item-card',
  standalone: true,
  template: '<div class="stub-library-card"></div>'
})
class StubLibraryItemCardComponent {
  @Input() item: unknown;
  @Input() showFavorite = false;
}

describe('CrewDashboardPreviewComponent', () => {
  let fixture: ComponentFixture<CrewDashboardPreviewComponent>;
  let component: CrewDashboardPreviewComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CrewDashboardPreviewComponent]
    })
      .overrideComponent(CrewDashboardPreviewComponent, {
        set: {
          imports: [CommonModule, StubUserAvatarComponent, StubLibraryItemCardComponent]
        }
      })
      .compileComponents();

    fixture = TestBed.createComponent(CrewDashboardPreviewComponent);
    component = fixture.componentInstance;
    component.useSwipe = false;
    component.viewportWidth = 320;
    spyOn(component as unknown as { measureViewport: () => void }, 'measureViewport');
  });

  it('renders no preview body when preview is null', () => {
    component.preview = null;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.carousel-viewport')).toBeNull();
    expect(fixture.nativeElement.querySelector('.preview-root.has-preview')).toBeNull();
  });

  it('shows a skeleton while preview content is loading', () => {
    component.loading = true;
    component.preview = null;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.preview-root.has-preview')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.preview-skeleton')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.carousel-viewport')).toBeNull();
  });

  it('frames the projected header when a preview is present', () => {
    const item = {
      id: 12,
      authorUsername: 'Ritu',
      lastActivityAt: new Date('2026-01-01T12:00:00Z'),
      title: 'Hello',
      body: 'Body text'
    };
    const preview: CrewDashboardPreview = {
      kind: 'forum',
      items: [item],
      hasMore: false
    };
    component.preview = preview;
    component.ngOnChanges({
      preview: {
        currentValue: preview,
        previousValue: null,
        firstChange: true,
        isFirstChange: () => true
      }
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.preview-root.has-preview')).toBeTruthy();

    const spy = jasmine.createSpy('open');
    component.open.subscribe(spy);
    fixture.nativeElement.querySelector('.carousel-slide')?.click();
    expect(spy).toHaveBeenCalledWith(item);
  });

  it('renders a gift message preview', () => {
    component.preview = {
      kind: 'gift',
      items: [{
        id: 3,
        message: 'Sent $10 via Venmo',
        categoryLabel: 'Cycle',
        timestamp: new Date('2026-02-01T10:00:00Z'),
        unverified: true
      }],
      hasMore: false
    };
    component.ngOnChanges({
      preview: {
        currentValue: component.preview,
        previousValue: null,
        firstChange: true,
        isFirstChange: () => true
      }
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sent $10 via Venmo');
    expect(fixture.nativeElement.textContent).toContain('Cycle');
    expect(fixture.nativeElement.textContent).toContain('Unverified');
  });

  it('opens the active library carousel item and cycles with next/previous controls', () => {
    component.useSwipe = false;
    component.preview = {
      kind: 'library',
      hasMore: false,
      items: [
        {
          libraryKind: 'unit',
          unit: {
            unitId: 11,
            offeringId: 5,
            holderUserId: 2,
            holderUsername: 'Ada',
            title: 'Drill',
            descriptionPreview: 'Cordless',
            categories: []
          }
        },
        {
          libraryKind: 'quest',
          task: {
            taskId: 22,
            title: 'Paint fence',
            creatorUsername: 'Kai',
            creatorUserId: 3,
            value: 15,
            scheduleSummary: 'Soon'
          },
          dueLabel: 'Soon'
        }
      ]
    };
    component.ngOnChanges({
      preview: {
        currentValue: component.preview,
        previousValue: null,
        firstChange: true,
        isFirstChange: () => true
      }
    });
    component.carousel.setItemCount(2);
    component.carousel.reset(0);
    fixture.detectChanges();

    const spy = jasmine.createSpy('open');
    component.open.subscribe(spy);

    const buttons = fixture.nativeElement.querySelectorAll('.carousel-button');
    expect(buttons.length).toBe(2);
    expect(fixture.nativeElement.querySelector('.carousel-slide .carousel-button')).toBeNull();

    fixture.nativeElement.querySelector('.carousel-slide')?.click();
    expect(spy).toHaveBeenCalledWith(jasmine.objectContaining({
      libraryKind: 'unit'
    }));

    buttons[1].click();
    fixture.detectChanges();
    expect(component.activeIndex).toBe(1);
    fixture.nativeElement.querySelectorAll('.carousel-slide')[1]?.click();
    expect(spy).toHaveBeenCalledWith(jasmine.objectContaining({
      libraryKind: 'quest'
    }));
  });
});
