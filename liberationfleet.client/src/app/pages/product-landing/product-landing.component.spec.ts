import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ProductLandingComponent } from './product-landing.component';

describe('ProductLandingComponent', () => {
  let fixture: ComponentFixture<ProductLandingComponent>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.returnValue(Promise.resolve(true));

    await TestBed.configureTestingModule({
      imports: [ProductLandingComponent],
      providers: [{ provide: Router, useValue: router }]
    }).compileComponents();

    fixture = TestBed.createComponent(ProductLandingComponent);
    fixture.detectChanges();
  });

  it('should create and display landing content', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('LiberationFleet');
    expect(element.textContent).toContain('Product Landing Working');
  });

  it('should navigate to sign-in when primary button is clicked', () => {
    fixture.componentInstance.signInButton.onClick?.();
    expect(router.navigate).toHaveBeenCalledWith(['/sign-in']);
  });
});
