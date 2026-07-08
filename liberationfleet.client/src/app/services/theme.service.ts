import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { APP_THEMES, AppThemeDefinition, AppThemeId, DEFAULT_APP_THEME } from '../models/theme.model';

const STORAGE_KEY = 'lf-app-theme';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private readonly themeSubject = new BehaviorSubject<AppThemeId>(DEFAULT_APP_THEME);
  readonly theme$ = this.themeSubject.asObservable();
  readonly themes: AppThemeDefinition[] = APP_THEMES;

  init(): void {
    this.applyTheme(this.getStoredTheme(), false);
  }

  get currentTheme(): AppThemeId {
    return this.themeSubject.value;
  }

  getThemeDefinition(themeId: AppThemeId): AppThemeDefinition {
    return APP_THEMES.find(theme => theme.id === themeId) ?? APP_THEMES[0];
  }

  applyTheme(themeId: AppThemeId, persist = true): void {
    const resolved = APP_THEMES.some(theme => theme.id === themeId) ? themeId : DEFAULT_APP_THEME;
    document.documentElement.setAttribute('data-theme', resolved);
    this.themeSubject.next(resolved);

    if (persist) {
      localStorage.setItem(STORAGE_KEY, resolved);
    }
  }

  private getStoredTheme(): AppThemeId {
    const stored = localStorage.getItem(STORAGE_KEY) as AppThemeId | null;
    return stored && APP_THEMES.some(theme => theme.id === stored) ? stored : DEFAULT_APP_THEME;
  }
}
