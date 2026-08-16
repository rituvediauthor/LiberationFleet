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

  /**
   * Download plain-media bytes for playback. Uses the same URL the video
   * element uses (including ?access_token=) via fetch — not HttpClient — so
   * Capacitor/native absolute API origins and session auth stay aligned with
   * progressive streaming (no relative-/api rewrite, no Bearer-only path).
   */
  fetchPlainMediaBlob(streamUrl: string, mimeHint = 'video/mp4'): Observable<Blob> {
    return new Observable<Blob>(subscriber => {
      const controller = new AbortController();
      void (async () => {
        try {
          const response = await fetch(streamUrl, {
            method: 'GET',
            signal: controller.signal,
            // Auth is the access_token query on streamUrl (same as <video src>).
            credentials: 'omit'
          });
          if (!response.ok) {
            throw new Error(`Media download failed (${response.status}).`);
          }
          const blob = await response.blob();
          if (!blob.size) {
            throw new Error('Media download returned an empty file.');
          }
          const type = (!blob.type || blob.type === 'application/octet-stream' || blob.type === 'binary/octet-stream')
            ? mimeHint
            : blob.type;
          subscriber.next(type === blob.type ? blob : blob.slice(0, blob.size, type));
          subscriber.complete();
        } catch (error) {
          if ((error as { name?: string })?.name === 'AbortError') {
            subscriber.complete();
            return;
          }
          subscriber.error(error);
        }
      })();
      return () => controller.abort();
    });
  }

  /** @see fetchPlainMediaBlob */
  fetchPlainMediaObjectUrl(streamUrl: string, mimeHint = 'video/mp4'): Observable<string> {
    return this.fetchPlainMediaBlob(streamUrl, mimeHint).pipe(
      map(blob => URL.createObjectURL(blob))
    );
  }

  /** Probe Content-Length for the plain-media stream (keeps access_token). */
  plainMediaContentLength(streamUrl: string): Observable<number | null> {
    return new Observable<number | null>(subscriber => {
      const controller = new AbortController();
      void (async () => {
        try {
          const response = await fetch(streamUrl, {
            method: 'HEAD',
            signal: controller.signal,
            credentials: 'same-origin'
          });
          if (!response.ok) {
            subscriber.next(null);
            subscriber.complete();
            return;
          }
          const raw = response.headers.get('Content-Length');
          const length = raw ? Number(raw) : NaN;
          subscriber.next(Number.isFinite(length) && length >= 0 ? length : null);
          subscriber.complete();
        } catch {
          subscriber.next(null);
          subscriber.complete();
        }
      })();
      return () => controller.abort();
    });
  }

  /** True when the plain-media endpoint advertises byte ranges (required for Safari play). */
  plainMediaAcceptsRanges(streamUrl: string): Observable<boolean> {
    return new Observable<boolean>(subscriber => {
      const controller = new AbortController();
      void (async () => {
        try {
          const response = await fetch(streamUrl, {
            method: 'HEAD',
            signal: controller.signal,
            credentials: 'same-origin'
          });
          const accept = (response.headers.get('Accept-Ranges') || '').toLowerCase();
          subscriber.next(response.ok && accept.includes('bytes'));
          subscriber.complete();
        } catch {
          subscriber.next(false);
          subscriber.complete();
        }
      })();
      return () => controller.abort();
    });
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
   * Envelope nonce / key version only (no ciphertext body).
   * Used to choose plain streaming vs encrypted full download.
   */
  getEncryptedContentMeta(
    contentType: EncryptedContentType,
    resourceId: string,
    crewId?: number | null,
    fleetId?: number | null
  ): Observable<{ resourceId: string; keyVersion: number; nonce: string }> {
    let params = new HttpParams()
      .set('contentType', contentType)
      .set('resourceId', resourceId);

    if (crewId != null) {
      params = params.set('crewId', crewId.toString());
    }

    if (fleetId != null) {
      params = params.set('fleetId', fleetId.toString());
    }

    return this.http.get<{ resourceId: string; keyVersion: number; nonce: string }>(
      `${this.apiUrl}/content/meta`,
      { params }
    );
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
