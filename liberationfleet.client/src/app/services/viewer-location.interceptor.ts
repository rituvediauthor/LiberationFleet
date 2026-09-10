import { Injectable } from '@angular/core';
import {
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest
} from '@angular/common/http';
import { Observable } from 'rxjs';
import { ProfileLocationService } from './profile-location.service';

/** Attaches ephemeral decrypted location for Local matching. Never persists server-side. */
@Injectable()
export class ViewerLocationInterceptor implements HttpInterceptor {
  constructor(private locationService: ProfileLocationService) {}

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const location = this.locationService.current;
    if (!location?.countryCode || !location.zipCode || !req.url.includes('/api/')) {
      return next.handle(req);
    }

    return next.handle(
      req.clone({
        setHeaders: {
          'X-LF-Viewer-Country': location.countryCode,
          'X-LF-Viewer-Postal': location.zipCode
        }
      })
    );
  }
}
