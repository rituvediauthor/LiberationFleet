import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class FallibleService {
  constructor(private http: HttpClient) {}

  recordClick(): Observable<void> {
    return this.http.post<void>('/api/fallible/click', {});
  }
}
