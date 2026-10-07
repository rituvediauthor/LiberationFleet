import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DonationCampaignPrompt,
  DonationCheckoutResponse,
  DonationStatus,
  DonationSummary
} from '../models/donation.model';

@Injectable({ providedIn: 'root' })
export class DonationService {
  private readonly apiUrl = '/api/donations';

  constructor(private http: HttpClient) {}

  getCampaignPrompt(variant: 'crew' | 'fleet'): Observable<DonationCampaignPrompt> {
    return this.http.get<DonationCampaignPrompt>(`${this.apiUrl}/campaign-prompt`, {
      params: { variant }
    });
  }

  acknowledgeCampaignPrompt(): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(
      `${this.apiUrl}/campaign-prompt/ack`,
      {}
    );
  }

  getStatus(): Observable<DonationStatus> {
    return this.http.get<DonationStatus>(`${this.apiUrl}/status`);
  }

  getSummary(): Observable<DonationSummary> {
    return this.http.get<DonationSummary>(`${this.apiUrl}/summary`);
  }

  createCheckout(amountCents: number, receiptEmail?: string | null): Observable<DonationCheckoutResponse> {
    return this.http.post<DonationCheckoutResponse>(`${this.apiUrl}/checkout`, {
      amountCents,
      receiptEmail: receiptEmail?.trim() || undefined
    });
  }
}
