import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-crew-find-panel',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './crew-find-panel.component.html',
  styleUrl: './crew-find-panel.component.css'
})
export class CrewFindPanelComponent {
  /** When true, copy reflects switching/joining from an existing crew. */
  @Input() hasCrew = false;
  @Input() loadError = false;

  @Output() retry = new EventEmitter<void>();
  @Output() createCrew = new EventEmitter<void>();
  @Output() joinCrew = new EventEmitter<void>();
  @Output() invitations = new EventEmitter<void>();
  @Output() joinRequests = new EventEmitter<void>();
  @Output() crewDashboard = new EventEmitter<void>();
  @Output() howToUse = new EventEmitter<void>();

  get subtitle(): string {
    if (this.hasCrew) {
      return 'Create a new crew or join another one. Creating a crew will leave your current crew.';
    }
    return "You're not in a crew yet. Create one or join an existing crew to get started.";
  }
}
