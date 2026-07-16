import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { DonationService } from './donation.service';

describe('DonationService', () => {
  let service: DonationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [DonationService]
    });
    service = TestBed.inject(DonationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('gets campaign prompts with variant query', () => {
    service.getCampaignPrompt('crew').subscribe(prompt => {
      expect(prompt.show).toBeTrue();
      expect(prompt.messageVariant).toBe('crew');
    });

    const req = httpMock.expectOne(r => r.url === '/api/donations/campaign-prompt');
    expect(req.request.params.get('variant')).toBe('crew');
    req.flush({
      show: true,
      messageVariant: 'crew',
      message: 'Support the app',
      donationsEnabled: true
    });
  });

  it('acknowledges campaign prompts', () => {
    service.acknowledgeCampaignPrompt().subscribe(result => {
      expect(result.success).toBeTrue();
    });

    const req = httpMock.expectOne('/api/donations/campaign-prompt/ack');
    expect(req.request.method).toBe('POST');
    req.flush({ success: true, message: 'ok' });
  });

  it('loads donation summary', () => {
    service.getSummary().subscribe(summary => {
      expect(summary.currentTaxYearTotalUsd).toBe(25);
    });

    const req = httpMock.expectOne('/api/donations/summary');
    req.flush({
      success: true,
      message: 'ok',
      currentTaxYear: 2026,
      previousTaxYear: 2025,
      currentTaxYearTotalUsd: 25,
      previousTaxYearTotalUsd: 0,
      donationsEnabled: true
    });
  });

  it('creates checkout sessions', () => {
    service.createCheckout(1000).subscribe(result => {
      expect(result.checkoutUrl).toContain('stripe');
    });

    const req = httpMock.expectOne('/api/donations/checkout');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ amountCents: 1000 });
    req.flush({
      success: true,
      message: 'ok',
      checkoutUrl: 'https://checkout.stripe.com/test'
    });
  });
});
