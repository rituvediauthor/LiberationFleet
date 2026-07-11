import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NavigationService } from '../../services/navigation.service';
import { PageLayoutComponent, ActionBarButton } from '../../components/page-layout/page-layout.component';
import { ToastService } from '../../components/toast/toast.component';
import { ThemeService } from '../../services/theme.service';
import { APP_THEMES, AppThemeId } from '../../models/theme.model';

@Component({
  selector: 'app-theme-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, PageLayoutComponent],
  templateUrl: './theme-settings.component.html',
  styleUrl: './theme-settings.component.css'
})
export class ThemeSettingsComponent implements OnInit {
  readonly themes = APP_THEMES;
  selectedTheme: AppThemeId = 'light';
  backButton!: ActionBarButton;

  private router = inject(Router);


  private navigation = inject(NavigationService);
  private themeService = inject(ThemeService);
  private toastService = inject(ToastService);

  ngOnInit() {
    this.selectedTheme = this.themeService.currentTheme;
    this.backButton = this.navigation.createBackButton(['/app/profile/preferences']);
  }

  onThemeSelected(themeId: AppThemeId) {
    if (this.selectedTheme === themeId) {
      return;
    }

    this.selectedTheme = themeId;
    this.themeService.applyTheme(themeId);
    this.toastService.success(`${this.themeService.getThemeDefinition(themeId).label} applied`);
  }
}
