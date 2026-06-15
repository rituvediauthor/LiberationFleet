import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface ActionBarButton {
  label: string;
  type: 'back' | 'primary' | 'secondary';
  disabled?: boolean;
  onClick?: () => void;
}

@Component({
  selector: 'app-page-layout',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './page-layout.component.html',
  styleUrl: './page-layout.component.css'
})
export class PageLayoutComponent {
  @Input() backButton: ActionBarButton | null = null;
  @Input() primaryButton: ActionBarButton | null = null;
}
