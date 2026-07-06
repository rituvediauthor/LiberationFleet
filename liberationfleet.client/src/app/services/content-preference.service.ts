import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import {
  AdultContentPreference,
  ContentPreferencesResponse,
  UpdateContentPreferencesRequest
} from '../models/content-preference.model';

@Injectable({
  providedIn: 'root'
})
export class ContentPreferenceService {
  private readonly apiUrl = '/api/profile/content-preferences';
  private readonly preferenceSubject = new BehaviorSubject<AdultContentPreference>('Block');
  private loaded = false;

  readonly preference$ = this.preferenceSubject.asObservable();

  constructor(private http: HttpClient) {}

  get preference(): AdultContentPreference {
    return this.preferenceSubject.value;
  }

  ensureLoaded(): Observable<ContentPreferencesResponse> {
    if (this.loaded) {
      return new Observable(observer => {
        observer.next({
          success: true,
          message: 'Cached',
          preferences: { adultContentPreference: this.preference }
        });
        observer.complete();
      });
    }

    return this.getPreferences();
  }

  getPreferences(): Observable<ContentPreferencesResponse> {
    return this.http.get<ContentPreferencesResponse>(this.apiUrl).pipe(
      tap(response => {
        if (response.success && response.preferences) {
          this.loaded = true;
          this.preferenceSubject.next(response.preferences.adultContentPreference);
        }
      })
    );
  }

  updatePreferences(request: UpdateContentPreferencesRequest): Observable<ContentPreferencesResponse> {
    return this.http.put<ContentPreferencesResponse>(this.apiUrl, request).pipe(
      tap(response => {
        if (response.success && response.preferences) {
          this.loaded = true;
          this.preferenceSubject.next(response.preferences.adultContentPreference);
        }
      })
    );
  }
}
