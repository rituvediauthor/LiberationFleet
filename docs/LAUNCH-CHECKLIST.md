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
3. **Email sender (SMTP)** — password reset end-to-end ([AZURE-GO-LIVE §7.5](./AZURE-GO-LIVE.md#75-wire-password-reset-email-smtp); Section B.1) — **required before Staging/Production will start** (app refuses `LogEmailSender` outside Development/Docker)  
4. **Stripe test on staging + local** — [DONATION-SETUP.md](./DONATION-SETUP.md) Parts A–C  
5. **LiveKit on staging** — [LIVEKIT-SETUP.md](./LIVEKIT-SETUP.md) Path B  
6. **Report vendor + NCMEC ESP** — [REPORT-VENDOR-WEBHOOK.md](./REPORT-VENDOR-WEBHOOK.md), [NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md); confirm AES key from Key Vault (no empty fallback in Staging/Prod)  
7. **Production Azure** — [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) Step 11 (self-contained: tfvars, apply, secrets, **email SMTP**, Stripe/LiveKit, optional domain, **manual** first deploy)  
8. **Verify + pause staging** — AZURE-GO-LIVE Steps 12–13 (destroy staging when idle so you mostly pay for production)  
9. **Stripe live + LiveKit production** — already covered inside Step 11.4; detail: DONATION-SETUP Part D; LIVEKIT-SETUP Path C  
10. **Native `apiBaseUrl` + sync** — AZURE-GO-LIVE Step 15 + [NATIVE-APPS.md](./NATIVE-APPS.md)  
11. **Internal TestFlight / Play internal** — [STORE-SUBMISSION.md](./STORE-SUBMISSION.md)  
12. **Store screenshots + review notes + submit**  
13. **Follow-up product** — MFA (TOTP), push (APNs/FCM), Sign in with Apple — [AZURE-GO-LIVE Step 16](./AZURE-GO-LIVE.md#step-16--auth--safety-service-hookups-mfa-push-ncmec)  
14. **GitHub access check** — AZURE-GO-LIVE Step 14 (public clone OK; strangers cannot push)

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
| **Why** | `RequestPasswordReset` already builds a token and calls `IEmailSender`. Staging/Production **require** SMTP or the API will not start. Local/Docker without `Email:SmtpHost` uses `LogEmailSender` (link in logs only). |
| **Register** | Azure Communication Services Email (SMTP) **or** SendGrid / Postmark / Amazon SES SMTP |
| **Steps** | Follow **[AZURE-GO-LIVE §7.5](./AZURE-GO-LIVE.md#75-wire-password-reset-email-smtp)** (staging) and **§11.4.6** (production): verify domain → SMTP host/user/password → App Service `Email__*` settings → smoke “Forgot password”. |
| **App settings** | `Email__SmtpHost`, `Email__SmtpPort` (usually `587`), `Email__SmtpUser`, `Email__SmtpPassword`, `Email__FromAddress`, `Email__FromName`, `Email__AppPublicBaseUrl` (SPA origin used in the reset link) |
| **Status today** | **Code ready** — must **wire SMTP** before staging/prod deploy |

### B.2 Two-factor authentication

| | |
|---|---|
| **Why** | Product-complete MFA (TOTP enroll + login challenge + recovery codes) is **not shipped**. Security settings show a “coming soon” note; API rejects enabling `TwoFactorEnabled` and always reports `mfaAvailable: false`. |
| **Register** | Prefer **TOTP** (no vendor) first; optional SMS via Twilio Verify / Azure ACS SMS later |
| **Steps (when building)** | 1) Enroll secrets per user. 2) Challenge on login when MFA enrolled. 3) Recovery codes. 4) Re-enable UI + `MfaAvailable`. See [AZURE-GO-LIVE Step 16](./AZURE-GO-LIVE.md#step-16--auth--safety-service-hookups-mfa-push-ncmec). |
| **Status today** | **Intentionally unavailable** until TOTP ships (not a silent stub) |

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

### B.11 Local discovery (country + postal allowlists)

| | |
|---|---|
| **How it works** | Local public crews/fleets match when the seeker’s **profile country + postal code** appear on the group’s allowlist. Offerings may optionally restrict by the same country + postal list (empty list = no postal gate). Online scope ignores postal lists. No third-party geocoder or radius math. |
| **Status today** | **Implemented** — alphanumeric postal codes (2–12), ISO country on profile / Local crew & fleet / offerings; `ZipCodeDistanceService` removed |
| **Launch checks** | Smoke Local create → profile country/postal → Find Local; confirm Online still finds without postal; confirm US vs other-country same code does **not** match |
| **Optional later** | Richer country/region UX, autocomplete for postal formats, or map-assisted “suggest nearby codes” (still allowlist-based, not distance APIs) |

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
| Auth (password) | Ready | Ready (same API) | Wire SMTP (B.1 / AZURE-GO-LIVE §7.5) |
| MFA | Hidden / rejected | Same | Implement TOTP then re-enable UI |
| Chat / forums / E2EE | Ready | Ready | — |
| Voice | Ready | Needs mic permissions | LiveKit Cloud |
| Donations | Ready | External Checkout | Stripe live + policy review |
| Reports / safety | Ready | Ready | Vendor key + NCMEC ESP (manual filing) |
| Push when backgrounded | N/A (web push later) | Missing | APNs/FCM (Step 16) |
| Local discovery | Ready | Ready | Profile country + postal; Local allowlists |

---

## D. Day-of-launch smoke test

- [ ] `https://production-host/` loads  
- [ ] Register / login  
- [ ] Password reset email arrives (SMTP wired; not LogEmailSender)  
- [ ] Chat send + image attach  
- [ ] Voice join (two clients)  
- [ ] Donation Checkout (small live or final test)  
- [ ] Create a content report; vendor/ops path works  
- [ ] Profile country + postal; Local crew Find matches allowlist (and fails across countries with the same code)  
- [ ] Native install from TestFlight / Play internal still talks to prod API  
