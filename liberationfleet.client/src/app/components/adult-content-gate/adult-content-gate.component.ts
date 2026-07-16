import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AccessibleDialogDirective } from '../../directives/accessible-dialog.directive';

@Component({
  selector: 'app-adult-content-gate',
  standalone: true,
  imports: [CommonModule, AccessibleDialogDirective],
  templateUrl: './adult-content-gate.component.html',
  styleUrl: './adult-content-gate.component.css'
})
export class AdultContentGateComponent {
  @Input() visible = false;

  @Output() confirmed = new EventEmitter<void>();
  @Output() declined = new EventEmitter<void>();

  readonly onEscape = () => this.declined.emit();

  onBackdropClick(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('dialog-backdrop')) {
      this.declined.emit();
    }
  }
}
