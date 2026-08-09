import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-attach-permission-note',
  standalone: true,
  template: `
    <p class="attach-permission-note">
      <i class="fa-solid fa-circle-info" aria-hidden="true"></i>
      You do not have permission to attach files to this {{ scope }}'s content.
    </p>
  `,
  styles: [`
    .attach-permission-note {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      margin: 0.25rem 0;
      font-size: 0.8125rem;
      color: var(--text-muted, #6b7280);
    }
    .attach-permission-note i {
      opacity: 0.8;
    }
  `]
})
export class AttachPermissionNoteComponent {
  @Input() scope: 'crew' | 'fleet' = 'crew';
}
