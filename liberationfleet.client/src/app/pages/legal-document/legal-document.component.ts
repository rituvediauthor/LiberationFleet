import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { NavigationService } from '../../services/navigation.service';

@Component({
  selector: 'app-legal-document',
  standalone: true,
  imports: [CommonModule, RouterLink, PageLayoutComponent],
  templateUrl: './legal-document.component.html',
  styleUrl: './legal-document.component.css'
})
export class LegalDocumentComponent implements OnInit {
  title = 'Legal';
  body = '';
  loading = true;
  loadError = false;
  backButton!: ActionBarButton;
  otherDocs: { path: string; label: string }[] = [];

  private readonly relatedDocs = [
    { path: '/privacy', label: 'Privacy Policy' },
    { path: '/terms', label: 'Terms of Use' },
    { path: '/community-standards', label: 'Community Standards' }
  ];

  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private navigation = inject(NavigationService);
  private destroyRef = inject(DestroyRef);

  ngOnInit() {
    this.backButton = this.navigation.createBackButton(['/']);
    this.route.data.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => {
      this.title = typeof data['documentTitle'] === 'string' ? data['documentTitle'] : 'Legal';
      const assetPath = typeof data['assetPath'] === 'string' ? data['assetPath'] : '';
      const currentPath = `/${this.route.snapshot.routeConfig?.path ?? ''}`;
      this.otherDocs = this.relatedDocs.filter(d => d.path !== currentPath);
      void this.load(assetPath);
    });
  }

  private async load(assetPath: string) {
    this.loading = true;
    this.loadError = false;
    this.body = '';
    if (!assetPath) {
      this.body = 'Document not found.';
      this.loadError = true;
      this.loading = false;
      return;
    }

    try {
      this.body = await firstValueFrom(this.http.get(assetPath, { responseType: 'text' }));
    } catch {
      this.body = 'Unable to load this document. Please try again later.';
      this.loadError = true;
    } finally {
      this.loading = false;
    }
  }
}
