import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';

@Component({
  selector: 'app-placeholder-page',
  standalone: true,
  imports: [CommonModule, PageLayoutComponent],
  templateUrl: './placeholder-page.component.html',
  styleUrl: './placeholder-page.component.css'
})
export class PlaceholderPageComponent implements OnInit {
  title = 'Coming Soon';
  message = 'This page is under construction.';
  backTo = '/app/crew';
  backButton!: ActionBarButton;

  private route = inject(ActivatedRoute);
  private router = inject(Router);

  ngOnInit() {
    const data = this.route.snapshot.data;
    this.title = data['title'] ?? this.title;
    this.message = data['message'] ?? this.message;
    this.backTo = data['backTo'] ?? this.backTo;
    const queryTitle = this.route.snapshot.queryParamMap.get('title');
    if (queryTitle) {
      this.title = queryTitle;
    }

    this.backButton = {
      label: '←',
      type: 'back',
      onClick: () => this.router.navigate([this.backTo])
    };
  }
}
