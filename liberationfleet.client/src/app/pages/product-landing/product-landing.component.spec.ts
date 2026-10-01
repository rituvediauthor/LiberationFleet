import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DevMutualAidService } from '../../components/dev-toolbar/dev-mutual-aid.service';
import { ToastService } from '../../components/toast/toast.component';
import { DevToolsService } from '../../services/dev-tools.service';
import { ProductLandingComponent } from './product-landing.component';

describe('ProductLandingComponent', () => {
  let fixture: ComponentFixture<ProductLandingComponent>;
  let router: jasmine.SpyObj<Router>;
  let devMutualAid: jasmine.SpyObj<DevMutualAidService>;
  let devTools: jasmine.SpyObj<DevToolsService>;
  let toastService: jasmine.SpyObj<ToastService>;

  beforeEach(async () => {
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.returnValue(Promise.resolve(true));
    devMutualAid = jasmine.createSpyObj<DevMutualAidService>('DevMutualAidService', ['resetApp']);
    devTools = jasmine.createSpyObj<DevToolsService>('DevToolsService', ['load']);
    toastService = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
    devTools.load.and.returnValue(of({ enabled: false }));

    await TestBed.configureTestingModule({
      imports: [ProductLandingComponent],
      providers: [
        { provide: Router, useValue: router },
        { provide: DevMutualAidService, useValue: devMutualAid },
        { provide: DevToolsService, useValue: devTools },
        { provide: ToastService, useValue: toastService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProductLandingComponent);
    fixture.detectChanges();
  });

  it('should create and display landing content', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Liberation Fleet');
    expect(element.textContent).toContain('Build resilient communities');
    expect(element.textContent).toContain('How to use this app');
    expect(element.textContent).toContain('financial sea monsters');
    expect(element.querySelector('app-brand-logo')).toBeTruthy();
    expect(element.querySelectorAll('.menu-link').length).toBeGreaterThan(0);
  });

  it('should navigate to a public how-to topic when a menu button is clicked', () => {
    fixture.componentInstance.openTopic('giving-season');
    expect(router.navigate).toHaveBeenCalledWith(['/how-to', 'giving-season']);
  });

  it('should navigate to sign-in when primary button is clicked', () => {
    fixture.componentInstance.signInButton.onClick?.();
    expect(router.navigate).toHaveBeenCalledWith(['/sign-in']);
  });

  it('should show the reset panel when dev tools are enabled', () => {
    devTools.load.and.returnValue(of({ enabled: true }));

    fixture = TestBed.createComponent(ProductLandingComponent);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Nuke all data/content');
  });

  it('should call resetApp and show a success toast', () => {
    devTools.load.and.returnValue(of({ enabled: true }));
    devMutualAid.resetApp.and.returnValue(of({ success: true, message: 'reset ok' }));

    fixture = TestBed.createComponent(ProductLandingComponent);
    fixture.detectChanges();

    fixture.componentInstance.confirmNuke();

    expect(devMutualAid.resetApp).toHaveBeenCalled();
    expect(toastService.success).toHaveBeenCalledWith('reset ok', 6000);
  });

  it('should show an error toast when reset fails', () => {
    devTools.load.and.returnValue(of({ enabled: true }));
    devMutualAid.resetApp.and.returnValue(throwError(() => ({ error: { message: 'boom' } })));

    fixture = TestBed.createComponent(ProductLandingComponent);
    fixture.detectChanges();

    fixture.componentInstance.confirmNuke();

    expect(toastService.error).toHaveBeenCalledWith('boom', 6000);
  });
});
