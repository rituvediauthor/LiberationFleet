import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type CollapsibleSectionVariant = 'default' | 'plain';

@Component({
  selector: 'app-collapsible-section',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './collapsible-section.component.html',
  styleUrl: './collapsible-section.component.css'
})
export class CollapsibleSectionComponent {
  @Input({ required: true }) title!: string;
  @Input() expanded = false;
  /** `plain` = muted centered label (dashboards); default matches settings accordions. */
  @Input() variant: CollapsibleSectionVariant = 'default';

  toggle(): void {
    this.expanded = !this.expanded;
  }
}
