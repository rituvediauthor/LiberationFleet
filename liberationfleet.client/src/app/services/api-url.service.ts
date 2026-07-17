import { Inject, Injectable } from '@angular/core';
import { APP_ENVIRONMENT, AppEnvironment } from '../config/app-environment';

/** Resolves absolute API / SignalR URLs for web (same-origin) and native shells. */
@Injectable({ providedIn: 'root' })
export class ApiUrlService {
  constructor(@Inject(APP_ENVIRONMENT) private readonly environment: AppEnvironment) {}

  /** Absolute origin with no trailing slash, or empty for same-origin web. */
  get apiBaseUrl(): string {
    const base = this.environment.apiBaseUrl?.trim() ?? '';
    return base.endsWith('/') ? base.slice(0, -1) : base;
  }

  /** Prefix relative `/api/...` paths when a native apiBaseUrl is configured. */
  resolveApi(path: string): string {
    if (!path.startsWith('/')) {
      return path;
    }
    const base = this.apiBaseUrl;
    return base ? `${base}${path}` : path;
  }

  /** SignalR hub URL (`/hubs/chat`, etc.). */
  resolveHub(hubPath: string): string {
    const path = hubPath.startsWith('/') ? hubPath : `/${hubPath}`;
    return this.resolveApi(path);
  }
}
