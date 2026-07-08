import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
import { ThemeService } from './app/services/theme.service';

bootstrapApplication(AppComponent, appConfig)
  .then(appRef => appRef.injector.get(ThemeService).init())
  .catch(err => console.error(err));

