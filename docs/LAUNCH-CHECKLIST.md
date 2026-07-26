# Launch checklist — Azure, App Store, Google Play & third-party services

Master go-live list for **web + iOS + Android**. Use the linked guides for click-by-click detail.

| Guide | Use when |
|-------|----------|
| [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) | Azure subscription → Terraform → pipeline → staging/prod URL |
| [DONATION-SETUP.md](./DONATION-SETUP.md) | Stripe: local → staging (test) → production (live) |
| [LIVEKIT-SETUP.md](./LIVEKIT-SETUP.md) | Local Docker voice or LiveKit Cloud |
| [NATIVE-APPS.md](./NATIVE-APPS.md) | Capacitor build / sync / device run |
| [STORE-SUBMISSION.md](./STORE-SUBMISSION.md) | Play Console & App Store Connect |
| [REPORT-VENDOR-WEBHOOK.md](./REPORT-VENDOR-WEBHOOK.md) | Moderation contractor API |
| [SAFETY-REPORTING.md](./SAFETY-REPORTING.md) | In-app reporting overview |
| [NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md) | CSAM escalation process |
| [MEDIA-DEEP-FREEZE.md](./MEDIA-DEEP-FREEZE.md) | Cold media storage |
| [JURISDICTION-ASSUMPTIONS.md](./JURISDICTION-ASSUMPTIONS.md) | US-first / 18+ assumptions |
| [NONPROFIT-ENTITY-SETUP.md](./NONPROFIT-ENTITY-SETUP.md) | Form a US nonprofit (categories, do’s/don’ts) |

---

## Suggested order of operations

1. **Legal** — entity, privacy/terms URLs, support emails (Section A)  
2. **Azure staging** — [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) through first verify (Steps 1–9)  
3. **Email sender** — password reset end-to-end (Section B.1; code still stubbed)  
4. **Stripe test on staging + local** — [DONATION-SETUP.md](./DONATION-SETUP.md) Parts A–C  
5. **LiveKit on staging** — [LIVEKIT-SETUP.md](./LIVEKIT-SETUP.md) Path B  
6. **Report vendor + NCMEC ESP** — [REPORT-VENDOR-WEBHOOK.md](./REPORT-VENDOR-WEBHOOK.md), [NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md)  
7. **Production Azure** — AZURE-GO-LIVE Step 11 (creates `lfleetproductionkv`, etc.)  
8. **Stripe live + LiveKit production** — DONATION-SETUP Part D; LIVEKIT-SETUP Path C  
9. **Native `apiBaseUrl` + sync** — [NATIVE-APPS.md](./NATIVE-APPS.md)  
10. **Internal TestFlight / Play internal** — [STORE-SUBMISSION.md](./STORE-SUBMISSION.md)  
11. **Store screenshots + review notes + submit**  
12. **Follow-up** — MFA, push, geocoding, Sign in with Apple  

---

## A. Legal & business (do first)

- [ ] Form legal entity (LLC / nonprofit / etc.) and bank account — nonprofit path: [NONPROFIT-ENTITY-SETUP.md](./NONPROFIT-ENTITY-SETUP.md)  
- [ ] Confirm US-first + 18+ assumptions with counsel ([JURISDICTION-ASSUMPTIONS.md](./JURISDICTION-ASSUMPTIONS.md))  
- [ ] Publish **Privacy Policy** URL (HTTPS) — draft: `liberationfleet.client/src/assets/privacy-policy.txt`  
- [ ] Publish **Terms of Use** URL — `.../terms-of-use.txt`  
- [ ] Publish **Community / Acceptable Use** — `.../community-standards.txt`  
- [ ] Age gate / 18+ disclosure aligned with store questionnaires  
- [ ] Designate `privacy@…` and `support@…` inboxes  
- [ ] DPA / vendor agreements for subprocessors that handle personal data  

---

## B. Third-party services — register & link

### B.1 Email (password reset, recovery, future OTP)

| | |
|---|---|
| **Why** | Password reset currently creates a token and **logs it** — no email is sent yet. |
| **Register** | Azure Communication Services Email **or** SendGrid / Postmark / Amazon SES |
| **Steps** | 1) Create sender domain + verify DNS (SPF/DKIM). 2) Create API key. 3) Store key in Key Vault. 4) Implement `IEmailSender` and wire `RequestPasswordReset` to send the link. 5) Set `Email__FromAddress`, `Email__AppBaseUrl`. |
| **Status today** | **Missing / stub** |

### B.2 Two-factor authentication

| | |
|---|---|
| **Why** | Security settings expose “two-factor” but login does **not** enforce MFA. |
| **Register** | Prefer **TOTP** (no vendor) first; optional SMS via Twilio Verify / Azure ACS SMS |
| **Steps** | 1) Enroll secrets per user. 2) Challenge on login when `TwoFactorEnabled`. 3) Recovery codes. |
| **Status today** | **Flag only — not enforced** |

### B.3 Donations (Stripe)

Follow **[DONATION-SETUP.md](./DONATION-SETUP.md)**. Keep staging and production separate.

- [ ] Stripe account + org bank payouts (Part A)
- [ ] **Local:** test keys in user-secrets (Part B)
- [ ] **Staging:** test keys + test webhook in staging Key Vault (Part C) — after Azure staging exists
- [ ] **Production:** live keys + live webhook in production Key Vault (Part D) — after AZURE-GO-LIVE Step 11

### B.4 Voice (LiveKit)

Follow **[LIVEKIT-SETUP.md](./LIVEKIT-SETUP.md)**.

- [ ] Local Docker voice (Path A), optional
- [ ] **Staging:** LiveKit Cloud + staging Key Vault (Path B)
- [ ] **Production:** LiveKit Cloud + production Key Vault (Path C) — after AZURE-GO-LIVE Step 11
- [ ] `livekit_host` in the matching `*.tfvars` + apply that environment only
- [ ] Key Vault `LiveKit-ApiKey` / `LiveKit-ApiSecret` per environment

### B.5 Content moderation / report triage

Follow **[REPORT-VENDOR-WEBHOOK.md](./REPORT-VENDOR-WEBHOOK.md)**.

- [ ] Choose internal ops and/or contractor  
- [ ] Generate long `ReportEvidence-VendorApiKey` → **staging** Key Vault first; repeat for **production** when ready  
- [ ] Optional `VendorNotifyUrl` HTTPS hook (per App Service)  
- [ ] Contractor can call `/api/reports/ops` with `X-Report-Vendor-Key`  

### B.6 NCMEC CyberTipline (CSAM)

Follow **[NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md)**.

- [ ] Register as ESP with NCMEC  
- [ ] Document who files and how `QueuedForNcmec` is handled  
- [ ] Tabletop / dry-run before launch  

### B.7 Apple Developer Program

- [ ] Enroll at [developer.apple.com](https://developer.apple.com) (Organization preferred)  
- [ ] App ID `com.liberationfleet.app`  
- [ ] Continue in [STORE-SUBMISSION.md](./STORE-SUBMISSION.md) § Apple  

### B.8 Google Play Console

- [ ] Register at [play.google.com/console](https://play.google.com/console)  
- [ ] Application ID `com.liberationfleet.app`  
- [ ] Continue in [STORE-SUBMISSION.md](./STORE-SUBMISSION.md) § Play  

### B.9 Push notifications (mobile)

| | |
|---|---|
| **Why** | SignalR only works while the app is open |
| **Register** | Apple APNs; Firebase Cloud Messaging; optional Azure Notification Hubs |
| **Status today** | **Not implemented** |

### B.10 Sign in with Apple / Google (optional)

| | |
|---|---|
| **Status today** | **Not implemented** (email/password + JWT only) |

### B.11 Geocoding / ZIP distance

| | |
|---|---|
| **Status today** | **Stub** (~15 hard-coded ZIPs) — replace `ZipCodeDistanceService` |

### B.12 Observability

- [ ] Confirm Application Insights resource exists (Terraform)  
- [ ] Open Live Metrics / failures after first staging deploy  
- [ ] Optional: thicken App Insights SDK usage in the app  

### B.13 Media deep freeze

- [ ] Confirm Terraform deep-freeze module applied  
- [ ] Read [MEDIA-DEEP-FREEZE.md](./MEDIA-DEEP-FREEZE.md)  
- [ ] Prod: `MediaDeepFreeze__Provider=azure`  

### B.14 Domain, DNS, TLS, email DNS

- [ ] Buy/configure domain  
- [ ] Custom domain + managed cert on App Service ([AZURE-GO-LIVE Step 10](./AZURE-GO-LIVE.md#step-10--custom-domain--tls))  
- [ ] SPF/DKIM/DMARC when email sender is live  
- [ ] Update Stripe `PublicAppBaseUrl` + CORS  

### B.15 Azure subscription & DevOps

Follow **[AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md)** Steps 1–12.

- [ ] Subscription + ADO project  
- [ ] Service connection `azure-liberationfleet`  
- [ ] Environments `staging` / `production` (approval on prod)  
- [ ] Terraform bootstrap + staging apply  
- [ ] Variable groups  
- [ ] Pipeline from `azure-pipelines.yml`  
- [ ] Production apply + approve deploy  

---

## C. Feature readiness matrix

| Feature | Web | iOS/Android | Blocker |
|---------|-----|-------------|---------|
| Auth (password) | Ready | Ready (same API) | Email for reset |
| MFA | UI stub | Same | Implement TOTP/SMS |
| Chat / forums / E2EE | Ready | Ready | — |
| Voice | Ready | Needs mic permissions | LiveKit Cloud |
| Donations | Ready | External Checkout | Stripe live + policy review |
| Reports / safety | Ready | Ready | Vendor + NCMEC ESP |
| Push when backgrounded | N/A (web push later) | Missing | APNs/FCM |
| Local discovery | Partial | Partial | Real geocoder |

---

## D. Day-of-launch smoke test

- [ ] `https://production-host/` loads  
- [ ] Register / login  
- [ ] Password reset email (when sender exists)  
- [ ] Chat send + image attach  
- [ ] Voice join (two clients)  
- [ ] Donation Checkout (small live or final test)  
- [ ] Create a content report; vendor/ops path works  
- [ ] Native install from TestFlight / Play internal still talks to prod API  
