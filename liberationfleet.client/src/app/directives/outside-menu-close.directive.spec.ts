import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OutsideMenuCloseDirective } from './outside-menu-close.directive';

@Component({
  standalone: true,
  imports: [OutsideMenuCloseDirective],
  template: `
    <ng-container appOutsideMenuClose (appOutsideMenuClose)="closed = true"></ng-container>
    <button type="button" class="kebab" aria-haspopup="menu">Options</button>
    <div class="menu" role="menu">
      <button type="button" class="item">Mute</button>
    </div>
    <button type="button" class="outside">Outside</button>
  `
})
class HostComponent {
  closed = false;
}

describe('OutsideMenuCloseDirective', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('closes when a click outside the menu stops propagation', () => {
    const outside = fixture.nativeElement.querySelector('.outside') as HTMLButtonElement;
    outside.addEventListener('click', event => event.stopPropagation());
    outside.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(fixture.componentInstance.closed).toBeTrue();
  });

  it('leaves the kebab button click to the toggle handler', () => {
    const kebab = fixture.nativeElement.querySelector('.kebab') as HTMLButtonElement;
    kebab.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(fixture.componentInstance.closed).toBeFalse();
  });

  it('leaves clicks inside the menu to the menu item handler', () => {
    const item = fixture.nativeElement.querySelector('.item') as HTMLButtonElement;
    item.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(fixture.componentInstance.closed).toBeFalse();
  });
});
