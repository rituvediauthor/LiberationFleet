import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AccessibleDialogDirective } from '../../directives/accessible-dialog.directive';
import { MUTE_DURATION_OPTIONS, MuteDuration } from '../../models/notification.model';

@Component({
  selector: 'app-mute-duration-dialog',
  standalone: true,
  imports: [CommonModule, AccessibleDialogDirective],
  templateUrl: './mute-duration-dialog.component.html',
  styleUrl: './mute-duration-dialog.component.css'
})
export class MuteDurationDialogComponent {
  @Input() visible = false;
  @Output() durationChosen = new EventEmitter<MuteDuration>();
  @Output() cancelMute = new EventEmitter<void>();
  @Output() dismissed = new EventEmitter<void>();

  readonly options = MUTE_DURATION_OPTIONS;

  choose(duration: MuteDuration) {
    this.durationChosen.emit(duration);
  }

  onCancelMute() {
    this.cancelMute.emit();
  }

  onDismiss() {
    this.dismissed.emit();
  }
}
