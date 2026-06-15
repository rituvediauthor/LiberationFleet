import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PageLayoutComponent } from './page-layout.component';

@Component({
  standalone: true,
  imports: [PageLayoutComponent],
  template: `<app-page-layout><p class="projected">Hello</p></app-page-layout>`
})
class HostComponent {}

describe('PageLayoutComponent', () => {
  let fixture: ComponentFixture<PageLayoutComponent>;
  let component: PageLayoutComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PageLayoutComponent, HostComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(PageLayoutComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should project content into page layout', () => {
    const hostFixture = TestBed.createComponent(HostComponent);
    hostFixture.detectChanges();

    expect(hostFixture.nativeElement.querySelector('.projected')?.textContent).toContain('Hello');
  });

  it('should render back and primary buttons when configured', () => {
    const backClick = jasmine.createSpy('backClick');
    const primaryClick = jasmine.createSpy('primaryClick');

    component.backButton = { label: '←', type: 'back', onClick: backClick };
    component.primaryButton = { label: 'Continue', type: 'primary', onClick: primaryClick };
    fixture.detectChanges();

    const backBtn = fixture.nativeElement.querySelector('.back-button') as HTMLButtonElement;
    const primaryBtn = fixture.nativeElement.querySelector('.primary-button') as HTMLButtonElement;

    expect(backBtn).toBeTruthy();
    expect(backBtn.querySelector('.fa-angle-left')).toBeTruthy();
    expect(primaryBtn.textContent).toContain('Continue');

    backBtn.click();
    primaryBtn.click();

    expect(backClick).toHaveBeenCalled();
    expect(primaryClick).toHaveBeenCalled();
  });

  it('should disable buttons when configured', () => {
    component.backButton = { label: '←', type: 'back', disabled: true };
    component.primaryButton = { label: 'Save', type: 'primary', disabled: true };
    fixture.detectChanges();

    expect((fixture.nativeElement.querySelector('.back-button') as HTMLButtonElement).disabled).toBeTrue();
    expect((fixture.nativeElement.querySelector('.primary-button') as HTMLButtonElement).disabled).toBeTrue();
  });

  it('should not render buttons when inputs are null', () => {
    component.backButton = null;
    component.primaryButton = null;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.back-button')).toBeFalsy();
    expect(fixture.nativeElement.querySelector('.primary-button')).toBeFalsy();
  });
});
