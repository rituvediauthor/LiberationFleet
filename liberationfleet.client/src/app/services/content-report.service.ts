import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  CreateContentReportRequest,
  CreateContentReportResponse,
  ReportEvidenceSnapshot
} from '../models/content-report.model';

@Injectable({
  providedIn: 'root'
})
export class ContentReportService {
  private readonly apiUrl = '/api/reports';

  constructor(private http: HttpClient) {}

  create(request: CreateContentReportRequest): Observable<CreateContentReportResponse> {
    return this.http.post<CreateContentReportResponse>(this.apiUrl, {
      reason: request.reason,
      targetType: request.targetType,
      targetResourceId: request.targetResourceId ?? null,
      targetParentId: request.targetParentId ?? null,
      targetAuthorUserId: request.targetAuthorUserId ?? null,
      crewId: request.crewId ?? null,
      fleetId: request.fleetId ?? null,
      reporterNote: request.reporterNote ?? null,
      evidencePlaintextJson: request.evidencePlaintextJson,
      alsoBlockAuthor: request.alsoBlockAuthor ?? false
    });
  }

  buildEvidenceJson(snapshot: ReportEvidenceSnapshot): string {
    const text = (snapshot.text ?? '').slice(0, 8000);
    const title = (snapshot.title ?? '').slice(0, 500);
    return JSON.stringify({
      text,
      title,
      authorUsername: snapshot.authorUsername ?? null,
      mediaResourceIds: snapshot.mediaResourceIds ?? [],
      attestation: snapshot.attestation,
      reportedAtClient: snapshot.reportedAtClient
    });
  }
}
