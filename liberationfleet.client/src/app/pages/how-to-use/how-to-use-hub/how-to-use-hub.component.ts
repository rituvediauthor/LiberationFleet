import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NavigationService } from '../../../services/navigation.service';
import { HOW_TO_USE_TOPICS, HOW_TO_USE_WELCOME } from '../how-to-use-content';

@Component({
  selector: 'app-how-to-use-hub',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './how-to-use-hub.component.html',
  styleUrl: './how-to-use-hub.component.css'
})
export class HowToUseHubComponent implements OnInit {
  readonly welcome = HOW_TO_USE_WELCOME;
  readonly topics = HOW_TO_USE_TOPICS;
  backButton!: ActionBarButton;

  private router = inject(Router);
  private navigation = inject(NavigationService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/crew']);
  }

  openTopic(topicId: string) {
    this.router.navigate(['/app/how-to', topicId]);
  }
}
