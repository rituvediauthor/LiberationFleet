import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../../components/page-layout/page-layout.component';
import { NavigationService } from '../../../services/navigation.service';

@Component({
  selector: 'app-fleet-stub-page',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './fleet-stub-page.component.html',
  styleUrl: './fleet-stub-page.component.css'
})
export class FleetStubPageComponent implements OnInit {
  title = 'Coming Soon';
  message = 'This fleet page is under construction.';
  backButton!: ActionBarButton;

  private route = inject(ActivatedRoute);
  private navigation = inject(NavigationService);

  ngOnInit() {
    const data = this.route.snapshot.data;
    this.title = data['title'] ?? this.title;
    this.message = data['message'] ?? this.message;
    const backTo = data['backTo'] ?? '/app/fleet';
    this.backButton = this.navigation.createBackButton([backTo]);
  }
}
