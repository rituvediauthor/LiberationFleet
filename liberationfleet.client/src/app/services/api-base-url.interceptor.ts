import { Inject, Injectable } from '@angular/core';
import { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APP_ENVIRONMENT, AppEnvironment } from '../config/app-environment';

@Injectable()
export class ApiBaseUrlInterceptor implements HttpInterceptor {
  constructor(@Inject(APP_ENVIRONMENT) private readonly environment: AppEnvironment) {}

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const baseUrl = this.environment.apiBaseUrl?.trim();
    if (!baseUrl || !req.url.startsWith('/api/')) {
      return next.handle(req);
    }

    const normalizedBase = baseUrl.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
    return next.handle(req.clone({ url: `${normalizedBase}${req.url}` }));
  }
}
