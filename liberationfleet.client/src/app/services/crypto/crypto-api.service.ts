import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  CrewKeyState,
  CryptoOperationResponse,
  EncryptedContentEnvelope,
  EncryptedContentType,
  FleetKeyState,
  UserKeyBundle,
  UserPrivateKeyBackup
} from '../../models/crypto.model';

@Injectable({
  providedIn: 'root'
})
export class CryptoApiService {
  private readonly apiUrl = '/api/crypto';

  constructor(private http: HttpClient) {}

  upsertPublicKey(identityPublicKey: string, keyVersion = 1): Observable<CryptoOperationResponse> {
    return this.http.put<CryptoOperationResponse>(`${this.apiUrl}/keys/public`, {
      identityPublicKey,
      keyVersion
    });
  }

  getPublicKey(userId: number): Observable<UserKeyBundle> {
    return this.http.get<UserKeyBundle>(`${this.apiUrl}/keys/public/${userId}`);
  }

  getCrewPublicKeys(crewId: number): Observable<UserKeyBundle[]> {
    return this.http.get<UserKeyBundle[]>(`${this.apiUrl}/keys/public/crew/${crewId}`);
  }

  getFleetPublicKeys(fleetId: number): Observable<UserKeyBundle[]> {
    return this.http.get<UserKeyBundle[]>(`${this.apiUrl}/keys/public/fleet/${fleetId}`);
  }

  upsertPrivateKeyBackup(backup: UserPrivateKeyBackup): Observable<CryptoOperationResponse> {
    return this.http.put<CryptoOperationResponse>(`${this.apiUrl}/keys/backup`, backup);
  }

  getMyPrivateKeyBackup(): Observable<UserPrivateKeyBackup> {
    return this.http.get<UserPrivateKeyBackup>(`${this.apiUrl}/keys/backup`);
  }

  upsertCrewKeyDistribution(
    crewId: number,
    payload: {
      userId: number;
      keyVersion: number;
      wrappedCrewKey: string;
      wrapNonce: string;
    }
  ): Observable<CryptoOperationResponse> {
    return this.http.put<CryptoOperationResponse>(`${this.apiUrl}/crew-keys/${crewId}`, payload);
  }

  getCrewKeyState(crewId: number): Observable<CrewKeyState> {
    return this.http.get<CrewKeyState>(`${this.apiUrl}/crew-keys/${crewId}`);
  }

  upsertFleetKeyDistribution(
    fleetId: number,
    payload: {
      userId: number;
      keyVersion: number;
      wrappedFleetKey: string;
      wrapNonce: string;
    }
  ): Observable<CryptoOperationResponse> {
    return this.http.put<CryptoOperationResponse>(`${this.apiUrl}/fleet-keys/${fleetId}`, payload);
  }

  getFleetKeyState(fleetId: number): Observable<FleetKeyState> {
    return this.http.get<FleetKeyState>(`${this.apiUrl}/fleet-keys/${fleetId}`);
  }

  upsertEncryptedContent(payload: {
    contentType: EncryptedContentType;
    resourceId: string;
    crewId?: number | null;
    fleetId?: number | null;
    keyVersion: number;
    nonce: string;
    ciphertext: string;
  }): Observable<CryptoOperationResponse> {
    return this.http.put<CryptoOperationResponse>(`${this.apiUrl}/content`, payload);
  }

  getEncryptedContents(
    contentType: EncryptedContentType,
    resourceIds: string[],
    crewId?: number | null,
    fleetId?: number | null
  ): Observable<EncryptedContentEnvelope[]> {
    let params = new HttpParams()
      .set('contentType', contentType)
      .set('resourceIds', resourceIds.join(','));

    if (crewId != null) {
      params = params.set('crewId', crewId.toString());
    }

    if (fleetId != null) {
      params = params.set('fleetId', fleetId.toString());
    }

    return this.http.get<EncryptedContentEnvelope[]>(`${this.apiUrl}/content`, { params });
  }

  deleteAttachment(contentType: EncryptedContentType, resourceId: string, crewId: number): Observable<CryptoOperationResponse> {
    const params = new HttpParams()
      .set('contentType', contentType)
      .set('resourceId', resourceId)
      .set('crewId', crewId.toString());

    return this.http.delete<CryptoOperationResponse>(`${this.apiUrl}/content`, { params });
  }
}
