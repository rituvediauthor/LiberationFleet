import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import {
  LibraryCategory,
  LibraryCategoryListResponse,
  LibraryUnitListItem,
  LibraryUnitListResponse,
  LibraryUnitListPage,
  CreateLibraryOfferingRequest,
  LibraryOfferingOperationResponse,
  LibraryUnitDetail,
  LibraryUnitDetailResponse,
  CreateLibraryRequestPayload,
  UpdateLibraryRequestPayload,
  LibraryRequestOperationResponse,
  LibraryCompleteRequestResponse,
  LibraryRequestListItem,
  LibraryRequestListResponse,
  LibraryRequestDetail,
  LibraryRequestDetailResponse,
  LibraryRequestMessage,
  LibraryRequestMessageListResponse,
  LibraryRequestMessageOperationResponse,
  SendLibraryRequestMessagePayload,
  LibraryOfferingListItem,
  LibraryOfferingListResponse,
  LibraryOfferingListPage,
  UpdateLibraryOfferingRequest,
  RecordLibraryAcquisitionPayload,
  ReportLibraryUnitBrokenPayload,
  RecordLibraryMaintenancePayload,
  LibraryUnitOperationResponse,
  LibraryMaintenanceOperationResponse,
  LibraryOfferingKind
} from '../models/library.model';

@Injectable({
  providedIn: 'root'
})
export class LibraryService {
  private readonly basePath = '/api/library';

  constructor(private http: HttpClient) {}

  getCategories(options?: { inUseOnly?: boolean; kind?: LibraryOfferingKind | 'Durable' }): Observable<LibraryCategory[]> {
    let params = new HttpParams();
    if (options?.inUseOnly) {
      params = params.set('inUseOnly', 'true');
    }
    if (options?.kind) {
      params = params.set('kind', options.kind);
    }

    return this.http.get<LibraryCategoryListResponse>(`${this.basePath}/categories`, { params }).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load categories');
        }
        return response.items;
      })
    );
  }

  getDurableUnits(options?: {
    search?: string;
    categoryIds?: number[];
    limit?: number;
    offset?: number;
  }): Observable<LibraryUnitListPage> {
    return this.getUnitListPage('durable-units', options);
  }

  getConsumableUnits(options?: {
    search?: string;
    categoryIds?: number[];
    limit?: number;
    offset?: number;
  }): Observable<LibraryUnitListPage> {
    return this.getUnitListPage('consumable-units', options);
  }

  getServicesUnits(options?: {
    search?: string;
    categoryIds?: number[];
    limit?: number;
    offset?: number;
  }): Observable<LibraryUnitListPage> {
    return this.getUnitListPage('services-units', options);
  }

  private getUnitListPage(
    path: string,
    options?: { search?: string; categoryIds?: number[]; limit?: number; offset?: number }
  ): Observable<LibraryUnitListPage> {
    let params = new HttpParams();
    if (options?.search?.trim()) {
      params = params.set('search', options.search.trim());
    }
    for (const categoryId of options?.categoryIds ?? []) {
      params = params.append('categoryIds', categoryId.toString());
    }
    if (options?.limit) {
      params = params.set('limit', options.limit.toString());
    }
    if (options?.offset) {
      params = params.set('offset', options.offset.toString());
    }

    return this.http.get<LibraryUnitListResponse>(`${this.basePath}/${path}`, { params }).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load offerings');
        }
        return { items: response.items, hasMore: response.hasMore };
      })
    );
  }

  getMyOfferings(options?: { search?: string; limit?: number; offset?: number }): Observable<LibraryOfferingListPage> {
    let params = new HttpParams();
    if (options?.search?.trim()) {
      params = params.set('search', options.search.trim());
    }
    if (options?.limit) {
      params = params.set('limit', options.limit.toString());
    }
    if (options?.offset) {
      params = params.set('offset', options.offset.toString());
    }

    return this.http.get<LibraryOfferingListResponse>(`${this.basePath}/offerings/mine`, { params }).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load your offerings');
        }
        return { items: response.items, hasMore: response.hasMore };
      })
    );
  }

  updateOffering(offeringId: number, payload: UpdateLibraryOfferingRequest): Observable<LibraryOfferingOperationResponse> {
    return this.http.put<LibraryOfferingOperationResponse>(`${this.basePath}/offerings/${offeringId}`, payload);
  }

  deleteOffering(offeringId: number): Observable<LibraryOfferingOperationResponse> {
    return this.http.delete<LibraryOfferingOperationResponse>(`${this.basePath}/offerings/${offeringId}`);
  }

  getUnitDetail(unitId: number): Observable<LibraryUnitDetail> {
    return this.http.get<LibraryUnitDetailResponse>(`${this.basePath}/units/${unitId}`).pipe(
      map(response => {
        if (!response.success || !response.item) {
          throw new Error(response.message || 'Failed to load item');
        }
        return response.item;
      })
    );
  }

  getUnitActiveRequests(unitId: number): Observable<LibraryRequestListItem[]> {
    return this.http.get<LibraryRequestListResponse>(`${this.basePath}/units/${unitId}/requests`).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load active requests');
        }
        return response.items;
      })
    );
  }

  createRequest(unitId: number, payload: CreateLibraryRequestPayload): Observable<LibraryRequestOperationResponse> {
    return this.http.post<LibraryRequestOperationResponse>(`${this.basePath}/units/${unitId}/requests`, {
      ...payload,
      quantity: payload.quantity ?? 1,
      keyVersion: payload.keyVersion ?? 1
    });
  }

  recordAcquisition(
    unitId: number,
    payload: RecordLibraryAcquisitionPayload
  ): Observable<LibraryCompleteRequestResponse> {
    return this.http.post<LibraryCompleteRequestResponse>(`${this.basePath}/units/${unitId}/acquisitions`, {
      ...payload,
      quantity: payload.quantity ?? 1,
      keyVersion: payload.keyVersion ?? 1
    });
  }

  getIncomingRequests(): Observable<LibraryRequestListItem[]> {
    return this.http.get<LibraryRequestListResponse>(`${this.basePath}/requests/incoming`).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load incoming requests');
        }
        return response.items;
      })
    );
  }

  getMyRequests(): Observable<LibraryRequestListItem[]> {
    return this.http.get<LibraryRequestListResponse>(`${this.basePath}/requests/mine`).pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Failed to load requests');
        }
        return response.items;
      })
    );
  }

  getRequestDetail(requestId: number): Observable<LibraryRequestDetail> {
    return this.http.get<LibraryRequestDetailResponse>(`${this.basePath}/requests/${requestId}`).pipe(
      map(response => {
        if (!response.success || !response.item) {
          throw new Error(response.message || 'Failed to load request');
        }
        return response.item;
      })
    );
  }

  getRequestMessages(requestId: number, options?: { limit?: number; beforeMessageId?: number }): Observable<LibraryRequestMessageListResponse> {
    let params = new HttpParams();
    if (options?.limit) {
      params = params.set('limit', options.limit.toString());
    }
    if (options?.beforeMessageId) {
      params = params.set('beforeMessageId', options.beforeMessageId.toString());
    }

    return this.http.get<LibraryRequestMessageListResponse>(`${this.basePath}/requests/${requestId}/messages`, { params });
  }

  sendRequestMessage(requestId: number, payload: SendLibraryRequestMessagePayload): Observable<LibraryRequestMessageOperationResponse> {
    return this.http.post<LibraryRequestMessageOperationResponse>(`${this.basePath}/requests/${requestId}/messages`, {
      ...payload,
      keyVersion: payload.keyVersion ?? 1,
      mentionedUserIds: payload.mentionedUserIds ?? []
    });
  }

  updateRequest(requestId: number, payload: UpdateLibraryRequestPayload): Observable<LibraryRequestOperationResponse> {
    return this.http.put<LibraryRequestOperationResponse>(`${this.basePath}/requests/${requestId}`, {
      ...payload,
      keyVersion: payload.keyVersion ?? 1
    });
  }

  cancelRequest(requestId: number): Observable<LibraryRequestOperationResponse> {
    return this.http.post<LibraryRequestOperationResponse>(`${this.basePath}/requests/${requestId}/cancel`, {});
  }

  completeRequest(requestId: number): Observable<LibraryCompleteRequestResponse> {
    return this.http.post<LibraryCompleteRequestResponse>(`${this.basePath}/requests/${requestId}/complete`, {});
  }

  denyRequest(requestId: number): Observable<LibraryRequestOperationResponse> {
    return this.http.post<LibraryRequestOperationResponse>(`${this.basePath}/requests/${requestId}/deny`, {});
  }

  undenyRequest(requestId: number): Observable<LibraryRequestOperationResponse> {
    return this.http.post<LibraryRequestOperationResponse>(`${this.basePath}/requests/${requestId}/undeny`, {});
  }

  createOffering(payload: CreateLibraryOfferingRequest): Observable<LibraryOfferingOperationResponse> {
    return this.http.post<LibraryOfferingOperationResponse>(`${this.basePath}/offerings`, {
      ...payload,
      kind: payload.kind ?? 'Durable',
      fulfillmentMode: payload.fulfillmentMode ?? 'OnRequest',
      quantityNotApplicable: payload.quantityNotApplicable ?? false,
      keyVersion: payload.keyVersion ?? 1
    });
  }

  reportBroken(unitId: number, payload: ReportLibraryUnitBrokenPayload): Observable<LibraryUnitOperationResponse> {
    return this.http.post<LibraryUnitOperationResponse>(`${this.basePath}/units/${unitId}/report-broken`, {
      ...payload,
      keyVersion: payload.keyVersion ?? 1
    });
  }

  confirmBroken(unitId: number): Observable<LibraryUnitOperationResponse> {
    return this.http.post<LibraryUnitOperationResponse>(`${this.basePath}/units/${unitId}/confirm-broken`, {});
  }

  reportFixed(unitId: number): Observable<LibraryUnitOperationResponse> {
    return this.http.post<LibraryUnitOperationResponse>(`${this.basePath}/units/${unitId}/report-fixed`, {});
  }

  reportLost(unitId: number): Observable<LibraryUnitOperationResponse> {
    return this.http.post<LibraryUnitOperationResponse>(`${this.basePath}/units/${unitId}/report-lost`, {});
  }

  recordMaintenance(
    unitId: number,
    payload: RecordLibraryMaintenancePayload
  ): Observable<LibraryMaintenanceOperationResponse> {
    return this.http.post<LibraryMaintenanceOperationResponse>(`${this.basePath}/units/${unitId}/maintenance`, {
      ...payload,
      keyVersion: payload.keyVersion ?? 1
    });
  }
}
