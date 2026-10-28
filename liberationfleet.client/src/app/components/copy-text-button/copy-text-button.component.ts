import { Component, Input, inject } from '@angular/core';
import { ToastService } from '../toast/toast.component';

@Component({
  selector: 'app-copy-text-button',
  standalone: true,
  templateUrl: './copy-text-button.component.html',
  styleUrl: './copy-text-button.component.css'
})
export class CopyTextButtonComponent {
  @Input({ required: true }) value = '';
  @Input() label = 'text';

  private toastService = inject(ToastService);

  async copy(event: Event) {
    event.stopPropagation();
    event.preventDefault();
    const text = this.value?.trim();
    if (!text) {
      return;
    }

    try {
      await navigator.clipboard.writeText(text);
      this.toastService.success('Copied');
    } catch {
      this.toastService.error('Could not copy');
    }
  }
}
