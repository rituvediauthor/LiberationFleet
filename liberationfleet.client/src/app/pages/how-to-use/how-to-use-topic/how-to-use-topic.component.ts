import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NavigationService } from '../../../services/navigation.service';
import { getHowToTopic, HowToGuideTopic } from '../how-to-use-content';

@Component({
  selector: 'app-how-to-use-topic',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './how-to-use-topic.component.html',
  styleUrl: './how-to-use-topic.component.css'
})
export class HowToUseTopicComponent implements OnInit {
  topic: HowToGuideTopic | null = null;
  backButton!: ActionBarButton;

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private navigation = inject(NavigationService);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/app/how-to']);
    this.route.paramMap.subscribe(params => {
      const id = params.get('topicId') ?? '';
      this.topic = getHowToTopic(id) ?? null;
      if (!this.topic) {
        void this.router.navigate(['/app/how-to']);
      }
    });
  }
}
