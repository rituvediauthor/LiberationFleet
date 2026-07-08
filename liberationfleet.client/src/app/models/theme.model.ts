export type AppThemeId = 'light' | 'dark';

export interface AppThemeDefinition {
  id: AppThemeId;
  label: string;
  description: string;
}

export const APP_THEMES: AppThemeDefinition[] = [
  {
    id: 'light',
    label: 'Light Mode',
    description: 'Bright surfaces with crisp contrast for daytime use.'
  },
  {
    id: 'dark',
    label: 'Dark Mode',
    description: 'Dimmed backgrounds that are easier on the eyes at night.'
  }
];

export const DEFAULT_APP_THEME: AppThemeId = 'light';
