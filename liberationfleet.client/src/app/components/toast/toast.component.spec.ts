import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ToastContainerComponent, ToastService } from './toast.component';

describe('ToastService', () => {
  let service: ToastService;

  beforeEach(() => {
    service = new ToastService();
  });

  it('should add toast and emit through toasts$', (done) => {
    service.toasts$.subscribe(toasts => {
      if (toasts.length > 0) {
        expect(toasts[0].message).toBe('Hello');
        expect(toasts[0].type).toBe('info');
        done();
      }
    });

    service.show('Hello', 'info');
  });

  it('success, error, info, and warning should delegate to show', () => {
    spyOn(service, 'show');

    service.success('ok');
    service.error('bad');
    service.info('note');
    service.warning('careful');

    expect(service.show).toHaveBeenCalledWith('ok', 'success', undefined);
    expect(service.show).toHaveBeenCalledWith('bad', 'error', undefined);
    expect(service.show).toHaveBeenCalledWith('note', 'info', undefined);
    expect(service.show).toHaveBeenCalledWith('careful', 'warning', undefined);
  });

  it('remove should remove toast by id', () => {
    service.show('one', 'info', 0);
    const id = service['toasts'][0].id;

    service.remove(id);

    let latest: unknown[] = [];
    service.toasts$.subscribe(t => latest = t);
    expect(latest.length).toBe(0);
  });

  it('should auto-remove toast after duration', fakeAsync(() => {
    service.show('temporary', 'info', 1000);
    expect(service['toasts'].length).toBe(1);

    tick(1000);
    expect(service['toasts'].length).toBe(0);
  }));
});

describe('ToastContainerComponent', () => {
  let fixture: ComponentFixture<ToastContainerComponent>;
  let toastService: ToastService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ToastContainerComponent],
      providers: [ToastService]
    }).compileComponents();

    fixture = TestBed.createComponent(ToastContainerComponent);
    toastService = TestBed.inject(ToastService);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should display toast message when service shows toast', () => {
    toastService.success('Saved successfully');
    fixture.detectChanges();

    const message = fixture.nativeElement.querySelector('.toast-message');
    expect(message?.textContent).toContain('Saved successfully');
    expect(fixture.nativeElement.querySelector('.toast-success')).toBeTruthy();
  });

  it('getIcon should return correct icons', () => {
    const component = fixture.componentInstance;
    expect(component.getIcon('success')).toBe('✓');
    expect(component.getIcon('error')).toBe('✕');
    expect(component.getIcon('warning')).toBe('⚠');
    expect(component.getIcon('info')).toBe('ℹ');
  });

  it('close should remove toast', () => {
    toastService.show('close me', 'info', 0);
    fixture.detectChanges();

    const id = toastService['toasts'][0].id;
    fixture.componentInstance.close(id);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.toast')).toBeFalsy();
  });
});
