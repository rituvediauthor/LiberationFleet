import { InjectionToken } from '@angular/core';

export interface AppEnvironment {
  production: boolean;
  apiBaseUrl: string;
}

export const APP_ENVIRONMENT = new InjectionToken<AppEnvironment>('app.environment');
