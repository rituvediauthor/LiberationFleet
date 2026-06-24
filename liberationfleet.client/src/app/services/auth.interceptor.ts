import { Injectable } from '@angular/core';
import { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AppStorageService, StorageScope } from './storage/app-storage.service';
import { AUTH_TOKEN_STORAGE_KEY } from './storage/storage-keys';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  constructor(private storage: AppStorageService) {}

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const token = this.storage.get(StorageScope.Persistent, AUTH_TOKEN_STORAGE_KEY);
    if (token) {
      req = req.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`
        }
      });
    }
    return next.handle(req);
  }
}
