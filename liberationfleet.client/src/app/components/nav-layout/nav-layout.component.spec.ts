import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NavLayoutComponent } from './nav-layout.component';

describe('NavLayoutComponent', () => {
  let fixture: ComponentFixture<NavLayoutComponent>;
  let component: NavLayoutComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NavLayoutComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(NavLayoutComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create with crew as default active tab', () => {
    expect(component).toBeTruthy();
    expect(component.activeTab).toBe('crew');
  });

  it('should render bottom navigation links', () => {
    const links = fixture.nativeElement.querySelectorAll('.nav-item');
    expect(links.length).toBe(3);
    expect(links[0].getAttribute('href')).toContain('/app/crew');
    expect(links[1].getAttribute('href')).toContain('/app/friends');
    expect(links[2].getAttribute('href')).toContain('/app/profile');
  });

  it('should project content into nav layout', () => {
    fixture.nativeElement.querySelector('.nav-content-inner')!.innerHTML = '<p class="projected">Hello</p>';
    expect(fixture.nativeElement.querySelector('.projected')?.textContent).toContain('Hello');
  });
});
