import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
import { ThemeService } from './app/services/theme.service';
import { initializeNativeShell } from './app/native/native-shell';

bootstrapApplication(AppComponent, appConfig)
  .then(async appRef => {
    appRef.injector.get(ThemeService).init();
    await initializeNativeShell();
  })
  .catch(err => console.error(err));

