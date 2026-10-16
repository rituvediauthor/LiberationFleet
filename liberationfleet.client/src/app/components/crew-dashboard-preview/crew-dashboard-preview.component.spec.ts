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
  });

  it('renders no preview body when preview is null', () => {
    component.preview = null;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.dash-preview')).toBeNull();
    expect(fixture.nativeElement.querySelector('.preview-root.has-preview')).toBeNull();
  });

  it('shows a skeleton while preview content is loading', () => {
    component.loading = true;
    component.preview = null;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.preview-root.has-preview')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.preview-skeleton')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.dash-preview')).toBeNull();
  });

  it('frames the projected header when a preview is present', () => {
    const preview: CrewDashboardPreview = {
      kind: 'forum',
      id: 12,
      authorUsername: 'Ritu',
      lastActivityAt: new Date('2026-01-01T12:00:00Z'),
      title: 'Hello',
      body: 'Body text'
    };
    component.preview = preview;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.preview-root.has-preview')).toBeTruthy();

    const spy = jasmine.createSpy('open');
    component.open.subscribe(spy);
    fixture.nativeElement.querySelector('.dash-preview')?.click();
    expect(spy).toHaveBeenCalledWith(preview);
  });

  it('renders a gift message preview', () => {
    component.preview = {
      kind: 'gift',
      id: 3,
      message: 'Sent $10 via Venmo',
      categoryLabel: 'Cycle',
      timestamp: new Date('2026-02-01T10:00:00Z'),
      unverified: true
    };
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sent $10 via Venmo');
    expect(fixture.nativeElement.textContent).toContain('Cycle');
    expect(fixture.nativeElement.textContent).toContain('Unverified');
  });
});
