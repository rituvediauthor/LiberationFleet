# Launch checklist — Azure, App Store, Google Play & third-party services

Use this as the master go-live list for **web + iOS + Android**.

Related guides:

- [NATIVE-APPS.md](./NATIVE-APPS.md) — Capacitor build / sync
- [STORE-SUBMISSION.md](./STORE-SUBMISSION.md) — App Store & Play step-by-step
- [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) — Azure + Terraform + pipeline
- Existing feature docs: `DONATION-SETUP.md`, `LIVEKIT-SETUP.md`, `SAFETY-REPORTING.md`, `NCMEC-CSAM-runbook.md`, `MEDIA-DEEP-FREEZE.md`, `REPORT-VENDOR-WEBHOOK.md`

---

## A. Legal & business (do first)

- [ ] Form legal entity (LLC / nonprofit / etc.) and bank account
- [ ] Confirm US-first + 18+ assumptions with counsel ([JURISDICTION-ASSUMPTIONS.md](./JURISDICTION-ASSUMPTIONS.md))
- [ ] Publish **Privacy Policy** URL (HTTPS) — draft text exists at `liberationfleet.client/src/assets/privacy-policy.txt`
- [ ] Publish **Terms of Use** URL — `.../terms-of-use.txt`
- [ ] Publish **Community / Acceptable Use** standards — `.../community-standards.txt`
- [ ] Age gate / 18+ disclosure aligned with store questionnaires
- [ ] Designate a privacy contact email (`privacy@…`) and support email (`support@…`)
- [ ] DPA / vendor agreements for any subprocessor that handles personal data

---

## B. Third-party services to register & link

### 1. Email (password reset, recovery, future 2FA OTP)

| | |
|---|---|
| **Why** | Password reset currently creates a token and **logs it** — no email is sent yet. Recovery and email OTP need a sender. |
| **Register** | Azure Communication Services Email **or** SendGrid / Postmark / Amazon SES |
| **Link to app** | Implement `IEmailSender`; wire `RequestPasswordReset` to send the reset link; store API key in Key Vault |
| **Status today** | **Missing / stub** |
| **Suggested env** | `Email__Provider`, `Email__ApiKey`, `Email__FromAddress`, `Email__AppBaseUrl` |

### 2. Two-factor authentication

| | |
|---|---|
| **Why** | Security settings expose “two-factor” but login does **not** enforce MFA. |
| **Register** | Prefer **TOTP authenticator apps** (no vendor) first; optional SMS via Twilio Verify / Azure ACS SMS |
| **Link to app** | Enroll secrets, challenge on login when `TwoFactorEnabled` |
| **Status today** | **Flag only — not enforced** |

### 3. Donations / payments (Stripe)

| | |
|---|---|
| **Why** | Platform donations via Checkout + webhook |
| **Register** | [Stripe](https://dashboard.stripe.com) account (activate live mode, bank payouts) |
| **Link to app** | Key Vault: `Stripe-SecretKey`, `Stripe-WebhookSecret`; webhook → `https://<host>/api/donations/stripe/webhook`; `Stripe__PublicAppBaseUrl` |
| **Status today** | **Implemented** — needs live keys ([DONATION-SETUP.md](./DONATION-SETUP.md)) |
| **Also** | Peer gifts use PayPal/Venmo/etc. **handles only** (no API). Users register those accounts themselves. |

### 4. Voice (LiveKit + TURN)

| | |
|---|---|
| **Why** | Crew voice rooms |
| **Register** | [LiveKit Cloud](https://cloud.livekit.io) (recommended) **or** self-host + coturn |
| **Link to app** | `LiveKit__Host` (wss), `LiveKit__ApiKey`, `LiveKit__ApiSecret` in Key Vault |
| **Status today** | **Implemented in code**; prod host not in Terraform ([LIVEKIT-SETUP.md](./LIVEKIT-SETUP.md)) |

### 5. Content moderation / report triage

| | |
|---|---|
| **Why** | Human review of abuse reports; optional outsourced vendor |
| **Register** | Internal ops inbox **and/or** a trusted moderation contractor; optional future: Hive / Spectrum / ActiveFence |
| **Link to app** | `ReportEvidence__VendorApiKey`, `VendorNotifyUrl`, AES key; ops UI via vendor webhook ([REPORT-VENDOR-WEBHOOK.md](./REPORT-VENDOR-WEBHOOK.md)) |
| **Status today** | **API ready** — need real vendor + key rotation |

### 6. NCMEC CyberTipline (CSAM)

| | |
|---|---|
| **Why** | US ESP reporting duty when CSAM is reported |
| **Register** | NCMEC ESP registration (portal; API later) |
| **Link to app** | Follow [NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md); queue status `QueuedForNcmec` |
| **Status today** | **Manual process** — no CyberTipline API client yet |

### 7. Apple Developer Program

| | |
|---|---|
| **Why** | Ship iOS App Store build |
| **Register** | [developer.apple.com](https://developer.apple.com) ($99/yr) — Organization account preferred |
| **Link** | Bundle ID `com.liberationfleet.app`; certificates, App Store Connect app record |
| **Status today** | Capacitor ready — account not in repo |

### 8. Google Play Console

| | |
|---|---|
| **Why** | Ship Android app |
| **Register** | [play.google.com/console](https://play.google.com/console) (one-time registration fee) |
| **Link** | Application ID `com.liberationfleet.app`; Play App Signing |
| **Status today** | Capacitor ready — account not in repo |

### 9. Push notifications (mobile)

| | |
|---|---|
| **Why** | Background alerts when app is closed (SignalR only works while open) |
| **Register** | Apple APNs (via Apple Developer); Firebase Cloud Messaging (Google); optional Azure Notification Hubs |
| **Link** | Future Capacitor Push plugin + server push sender |
| **Status today** | **Not implemented** |

### 10. Sign in with Apple / Google (optional)

| | |
|---|---|
| **Why** | Faster onboarding; Apple often expects SIWA if other social logins exist |
| **Register** | Apple Services ID; Google Cloud OAuth client |
| **Link** | New OAuth flow on server + client buttons |
| **Status today** | **Not implemented** (email/password + JWT only) |

### 11. Geocoding / ZIP distance

| | |
|---|---|
| **Why** | Local crew/fleet search |
| **Register** | Smarty, Google Geocoding, Azure Maps, or load a full ZCTA dataset |
| **Link** | Replace `ZipCodeDistanceService` stub |
| **Status today** | **Stub** (~15 hard-coded ZIPs) |

### 12. Observability

| | |
|---|---|
| **Why** | Crash/APM in production |
| **Register** | Azure Application Insights (Terraform already creates it) |
| **Link** | Enable App Insights SDK / agent (connection string already set on App Service) |
| **Status today** | **Infra yes / app SDK thin** |

### 13. Media deep freeze (Azure Blob)

| | |
|---|---|
| **Why** | Cold-store old chat/forum media ciphertext |
| **Register** | Included in Terraform `deep-freeze-storage` module |
| **Link** | `MediaDeepFreeze__*` + Key Vault connection string |
| **Status today** | **Implemented** ([MEDIA-DEEP-FREEZE.md](./MEDIA-DEEP-FREEZE.md)) |

### 14. Domain, DNS, TLS, email DNS

| | |
|---|---|
| **Why** | Branded web URL, Stripe redirects, email deliverability |
| **Register** | Domain registrar; Azure DNS or Cloudflare; SPF/DKIM/DMARC for mail |
| **Link** | Custom domain on App Service + managed cert; `Cors__AllowedOrigins`; Stripe `PublicAppBaseUrl` |

### 15. Azure subscription & DevOps

| | |
|---|---|
| **Why** | Host API+SPA, SQL, Key Vault, ACR, pipelines |
| **Register** | Azure subscription; Azure DevOps org (or GitHub Actions later) |
| **Link** | See [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) |

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

## D. Suggested order of operations

1. Legal + privacy/terms URLs live  
2. Azure bootstrap + staging deploy ([AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md))  
3. Email sender + password reset end-to-end  
4. Stripe live + webhook  
5. LiveKit Cloud  
6. Report vendor key + NCMEC ESP  
7. Set `environment.native.ts` `apiBaseUrl` → Capacitor sync  
8. Internal TestFlight / Play internal testing  
9. Store screenshots + review notes  
10. Production App Store / Play submission ([STORE-SUBMISSION.md](./STORE-SUBMISSION.md))  
11. (Follow-up) MFA, push, geocoding, SIWA  
