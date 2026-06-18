import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';

export interface DevToolsStatus {
  enabled: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class DevToolsService {
  private readonly apiUrl = '/api/dev/mutual-aid';
  private readonly enabledSubject = new BehaviorSubject(false);

  readonly enabled$ = this.enabledSubject.asObservable();

  constructor(private http: HttpClient) {}

  get isEnabled(): boolean {
    return this.enabledSubject.value;
  }

  load(): Observable<DevToolsStatus> {
    return this.http.get<DevToolsStatus>(`${this.apiUrl}/enabled`).pipe(
      tap(status => this.enabledSubject.next(status.enabled))
    );
  }
}
