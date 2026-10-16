import { Component, HostBinding, Injectable, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BehaviorSubject } from 'rxjs';

export interface Toast {
  id: string;
  message: string;
  type: 'success' | 'error' | 'info' | 'warning';
  duration?: number;
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  private toasts: Toast[] = [];
  private toastSubject = new BehaviorSubject<Toast[]>([]);
  public toasts$ = this.toastSubject.asObservable();
  /** When true, new toasts queue until resumed (e.g. crypto unlock dialog is open). */
  private paused = false;
  private queued: Array<{ message: string; type: Toast['type']; duration: number }> = [];
  private removalTimers = new Map<string, ReturnType<typeof setTimeout>>();

  /**
   * Pause toast display while a blocking overlay (crypto unlock) is shown.
   * Queued toasts flush when resumed.
   */
  setPaused(paused: boolean): void {
    if (this.paused === paused) {
      return;
    }
    this.paused = paused;
    if (paused) {
      this.clearRemovalTimers();
      this.toasts = [];
      this.toastSubject.next([]);
      return;
    }

    const pending = this.queued.splice(0, this.queued.length);
    for (const item of pending) {
      this.enqueue(item.message, item.type, item.duration);
    }
  }

  show(message: string, type: 'success' | 'error' | 'info' | 'warning' = 'info', duration?: number) {
    const resolvedDuration = duration ?? 3000;
    if (this.paused) {
      this.queued.push({ message, type, duration: resolvedDuration });
      return;
    }
    this.enqueue(message, type, resolvedDuration);
  }

  remove(id: string) {
    const timer = this.removalTimers.get(id);
    if (timer) {
      clearTimeout(timer);
      this.removalTimers.delete(id);
    }
    this.toasts = this.toasts.filter(t => t.id !== id);
    this.toastSubject.next([...this.toasts]);
  }

  success(message: string, duration?: number) {
    this.show(message, 'success', duration);
  }

  error(message: string, duration?: number) {
    this.show(message, 'error', duration);
  }

  info(message: string, duration?: number) {
    this.show(message, 'info', duration);
  }

  warning(message: string, duration?: number) {
    this.show(message, 'warning', duration);
  }

  private enqueue(message: string, type: Toast['type'], duration: number) {
    const id = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
    const toast: Toast = { id, message, type, duration };

    this.toasts.push(toast);
    this.toastSubject.next([...this.toasts]);

    if (duration > 0) {
      const timer = setTimeout(() => {
        this.removalTimers.delete(id);
        this.remove(id);
      }, duration);
      this.removalTimers.set(id, timer);
    }
  }

  private clearRemovalTimers() {
    for (const timer of this.removalTimers.values()) {
      clearTimeout(timer);
    }
    this.removalTimers.clear();
  }
}

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './toast.component.html',
  styleUrl: './toast.component.css'
})
export class ToastContainerComponent implements OnInit, OnDestroy {
  /** Keeps fixed toasts inside the visual viewport when the mobile keyboard opens. */
  @HostBinding('style.top.px') viewportTop = 0;

  private readonly onViewportChange = () => this.syncViewportOffset();

  constructor(public toastService: ToastService) {}

  get toasts$() {
    return this.toastService.toasts$;
  }

  ngOnInit() {
    this.syncViewportOffset();
    window.visualViewport?.addEventListener('resize', this.onViewportChange);
    window.visualViewport?.addEventListener('scroll', this.onViewportChange);
    window.addEventListener('resize', this.onViewportChange);
  }

  ngOnDestroy() {
    window.visualViewport?.removeEventListener('resize', this.onViewportChange);
    window.visualViewport?.removeEventListener('scroll', this.onViewportChange);
    window.removeEventListener('resize', this.onViewportChange);
  }

  close(id: string) {
    this.toastService.remove(id);
  }

  getIconClass(type: string): string {
    switch (type) {
      case 'success':
        return 'fa-circle-check';
      case 'error':
        return 'fa-circle-xmark';
      case 'warning':
        return 'fa-triangle-exclamation';
      default:
        return 'fa-circle-info';
    }
  }

  private syncViewportOffset() {
    const offsetTop = window.visualViewport?.offsetTop ?? 0;
    this.viewportTop = Math.max(0, Math.round(offsetTop));
  }
}
