import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';
import { ApplicationConfig, importProvidersFrom } from '@angular/core';
import { provideRouter, TitleStrategy, withInMemoryScrolling } from '@angular/router';
import { routes } from './app.routes';
import { AuthInterceptor } from './services/auth.interceptor';
import { ApiBaseUrlInterceptor } from './services/api-base-url.interceptor';
import { APP_ENVIRONMENT } from './config/app-environment';
import { environment } from '../environments/environment';
import { LiberationFleetTitleStrategy } from './services/liberation-fleet-title.strategy';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withInMemoryScrolling({ anchorScrolling: 'enabled' })),
    { provide: TitleStrategy, useClass: LiberationFleetTitleStrategy },
    { provide: APP_ENVIRONMENT, useValue: environment },
    importProvidersFrom(HttpClientModule),
    {
      provide: HTTP_INTERCEPTORS,
      useClass: ApiBaseUrlInterceptor,
      multi: true
    },
    {
      provide: HTTP_INTERCEPTORS,
      useClass: AuthInterceptor,
      multi: true
    }
  ]
};
