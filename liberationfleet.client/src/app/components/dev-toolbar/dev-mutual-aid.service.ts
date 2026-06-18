import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface DevActionResult {
  success: boolean;
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class DevMutualAidService {
  private readonly apiUrl = '/api/dev/mutual-aid';

  constructor(private http: HttpClient) {}

  newMonth(): Observable<DevActionResult> {
    return this.http.post<DevActionResult>(`${this.apiUrl}/new-month`, {});
  }

  newSeason(): Observable<DevActionResult> {
    return this.http.post<DevActionResult>(`${this.apiUrl}/new-season`, {});
  }

  completeCycles(): Observable<DevActionResult> {
    return this.http.post<DevActionResult>(`${this.apiUrl}/complete-cycles`, {});
  }

  resetSeason(): Observable<DevActionResult> {
    return this.http.post<DevActionResult>(`${this.apiUrl}/reset-season`, {});
  }

  recalculateCaps(): Observable<DevActionResult> {
    return this.http.post<DevActionResult>(`${this.apiUrl}/recalculate-caps`, {});
  }
}
