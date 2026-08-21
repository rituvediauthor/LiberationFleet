import { Component, OnInit, inject } from '@angular/core';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { NavigationService } from '../../services/navigation.service';

@Component({
  selector: 'app-ai-disclosure',
  standalone: true,
  imports: [PageLayoutComponent],
  templateUrl: './ai-disclosure.component.html',
  styleUrl: './ai-disclosure.component.css'
})
export class AiDisclosureComponent implements OnInit {
  backButton!: ActionBarButton;

  private navigation = inject(NavigationService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/profile']);
  }
}
