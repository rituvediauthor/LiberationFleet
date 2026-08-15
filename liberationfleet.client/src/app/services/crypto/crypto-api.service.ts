import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpEventType, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import {
  CrewKeyState,
  CryptoOperationResponse,
  EncryptedContentEnvelope,
  EncryptedContentType,
  FleetKeyState,
  UserKeyBundle,
  UserPrivateKeyBackup
} from '../../models/crypto.model';
import { ApiUrlService } from '../api-url.service';

@Injectable({
  providedIn: 'root'
})
export class CryptoApiService {
  private readonly apiUrl = '/api/crypto';
  private readonly apiUrls = inject(ApiUrlService);

  constructor(private http: HttpClient) {}

  /**
   * Progressive playback URL for unencrypted (`__plain__`) video/audio.
   * Uses `access_token` query so HTML5 media elements can Range-request without Authorization.
   */
  buildPlainMediaStreamUrl(options: {
    contentType: EncryptedContentType;
    resourceId: string;
    accessToken: string;
    crewId?: number | null;
    fleetId?: number | null;
  }): string {
    const params = new URLSearchParams();
    params.set('contentType', options.contentType);
    params.set('resourceId', options.resourceId);
    if (options.crewId != null && options.crewId > 0) {
      params.set('crewId', String(options.crewId));
    }
    if (options.fleetId != null && options.fleetId > 0) {
      params.set('fleetId', String(options.fleetId));
    }
    params.set('access_token', options.accessToken);
    return this.apiUrls.resolveApi(`${this.apiUrl}/content/plain-media?${params.toString()}`);
  }

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

  /** Same as upsertEncryptedContent but reports upload progress (0–100). */
  upsertEncryptedContentWithProgress(
    payload: {
      contentType: EncryptedContentType;
      resourceId: string;
      crewId?: number | null;
      fleetId?: number | null;
      keyVersion: number;
      nonce: string;
      ciphertext: string;
    },
    onProgress?: (percent: number) => void
  ): Observable<CryptoOperationResponse> {
    return new Observable<CryptoOperationResponse>(subscriber => {
      const sub = this.http.put<CryptoOperationResponse>(`${this.apiUrl}/content`, payload, {
        reportProgress: true,
        observe: 'events'
      }).subscribe({
        next: event => {
          if (event.type === HttpEventType.UploadProgress && event.total) {
            onProgress?.(Math.round((event.loaded / event.total) * 100));
          } else if (event.type === HttpEventType.Response) {
            if (event.body) {
              subscriber.next(event.body);
              subscriber.complete();
            } else {
              subscriber.error(new Error('Empty upload response.'));
            }
          }
        },
        error: err => subscriber.error(err),
        complete: () => subscriber.complete()
      });
      return () => sub.unsubscribe();
    });
  }

  /**
   * Binary AES-GCM ciphertext PUT for video/audio (no base64 JSON).
   * Prefer Blob body — avoids copying a multi‑MB ArrayBuffer (iOS OOM).
   * Nonce goes in X-LF-Nonce; other metadata as query params.
   */
  upsertEncryptedContentBytesWithProgress(
    payload: {
      contentType: EncryptedContentType;
      resourceId: string;
      crewId?: number | null;
      fleetId?: number | null;
      keyVersion: number;
      nonce: string;
      ciphertext: Blob | Uint8Array | ArrayBuffer;
    },
    onProgress?: (percent: number) => void
  ): Observable<CryptoOperationResponse> {
    let params = new HttpParams()
      .set('contentType', payload.contentType)
      .set('resourceId', payload.resourceId)
      .set('keyVersion', String(payload.keyVersion));

    if (payload.crewId != null) {
      params = params.set('crewId', payload.crewId.toString());
    }
    if (payload.fleetId != null) {
      params = params.set('fleetId', payload.fleetId.toString());
    }

    // Never ArrayBuffer.slice a huge typed array — that doubles peak RAM.
    const body: Blob = payload.ciphertext instanceof Blob
      ? payload.ciphertext
      : new Blob([payload.ciphertext as BlobPart], { type: 'application/octet-stream' });

    return new Observable<CryptoOperationResponse>(subscriber => {
      const sub = this.http.put<CryptoOperationResponse>(`${this.apiUrl}/content/bytes`, body, {
        params,
        headers: {
          'Content-Type': 'application/octet-stream',
          'X-LF-Nonce': payload.nonce
        },
        reportProgress: true,
        observe: 'events'
      }).subscribe({
        next: event => {
          if (event.type === HttpEventType.UploadProgress && event.total) {
            onProgress?.(Math.round((event.loaded / event.total) * 100));
          } else if (event.type === HttpEventType.Response) {
            if (event.body) {
              subscriber.next(event.body);
              subscriber.complete();
            } else {
              subscriber.error(new Error('Empty upload response.'));
            }
          }
        },
        error: err => subscriber.error(err),
        complete: () => subscriber.complete()
      });
      return () => sub.unsubscribe();
    });
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

  /**
   * Raw AES-GCM ciphertext bytes for one media resource (avoids multi‑MB JSON base64).
   * Nonce / keyVersion / resourceId come from X-LF-* response headers.
   */
  getEncryptedContentBytes(
    contentType: EncryptedContentType,
    resourceId: string,
    crewId?: number | null,
    fleetId?: number | null
  ): Observable<{
    resourceId: string;
    keyVersion: number;
    nonce: string;
    ciphertext: ArrayBuffer;
  }> {
    let params = new HttpParams()
      .set('contentType', contentType)
      .set('resourceId', resourceId);

    if (crewId != null) {
      params = params.set('crewId', crewId.toString());
    }

    if (fleetId != null) {
      params = params.set('fleetId', fleetId.toString());
    }

    return this.http.get(`${this.apiUrl}/content/bytes`, {
      params,
      observe: 'response',
      responseType: 'arraybuffer'
    }).pipe(
      map(response => {
        const nonce = response.headers.get('X-LF-Nonce');
        const keyVersionHeader = response.headers.get('X-LF-KeyVersion');
        const resolvedResourceId = response.headers.get('X-LF-ResourceId') || resourceId;
        if (!nonce || !response.body) {
          throw new Error('Encrypted content bytes response was incomplete.');
        }
        return {
          resourceId: resolvedResourceId,
          keyVersion: Number(keyVersionHeader) || 1,
          nonce,
          ciphertext: response.body
        };
      })
    );
  }

  deleteAttachment(contentType: EncryptedContentType, resourceId: string, crewId: number): Observable<CryptoOperationResponse> {
    const params = new HttpParams()
      .set('contentType', contentType)
      .set('resourceId', resourceId)
      .set('crewId', crewId.toString());

    return this.http.delete<CryptoOperationResponse>(`${this.apiUrl}/content`, { params });
  }
}
