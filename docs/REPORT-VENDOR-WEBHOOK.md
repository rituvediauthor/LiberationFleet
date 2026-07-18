# Report triage webhook (vendor / contractor) — step-by-step

Use this when you outsource review and do not want to inspect every report yourself.

Related: [SAFETY-REPORTING.md](./SAFETY-REPORTING.md), [NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md), [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md).

---

## Step 1 — Generate a vendor API key

1. Create a long random secret (32+ bytes), e.g.:
   ```bash
   openssl rand -base64 48
   ```
2. Store it securely (password manager). You will share it only with the contractor over a secure channel.

---

## Step 2 — Configure Azure (or local)

### Azure Key Vault

1. Portal → environment Key Vault → Secrets → `ReportEvidence-VendorApiKey`.
2. **New version** → paste the secret → Create.
3. Restart the Web App so it picks up the new version.

### Optional notify URL

If the contractor has an HTTPS webhook to receive “new report” pings:

1. App Service → Configuration → Application settings.
2. Set `ReportEvidence__VendorNotifyUrl` = `https://your-contractor.example/hooks/liberation-fleet-report`.
3. Optionally set `ReportEvidence__AutoEscalateNonCsamToVendor` = `true` when you are ready to auto-queue non-CSAM for vendor review.
4. Save + Restart.

### Local development

```powershell
dotnet user-secrets set "ReportEvidence:VendorApiKey" "<your-secret>" --project LiberationFleet.Server
```

---

## Step 3 — Give the contractor access

Share:

- API base URL (e.g. `https://your.domain`)
- Header name and key: `X-Report-Vendor-Key: <secret>`
- This document (endpoints + labels)
- Expectation: they POST labels; **you** still file NCMEC for CSAM until CyberTipline API is automated

---

## Step 4 — Contractor smoke test

1. Create a non-CSAM test report in the app (or have them wait for a real one).
2. Contractor calls:
   ```http
   GET /api/reports/ops?limit=50
   X-Report-Vendor-Key: <secret>
   ```
3. For a single report needing evidence:
   ```http
   GET /api/reports/ops?includeEvidence=true
   X-Report-Vendor-Key: <secret>
   ```
4. Contractor applies a label:
   ```http
   POST /api/reports/vendor/webhook
   X-Report-Vendor-Key: <secret>
   Content-Type: application/json

   {
     "reportId": 123,
     "label": "none",
     "notes": "Benign test"
   }
   ```
5. Confirm report status updated in your DB / ops view.

---

## Auth (reference)

All ops/vendor endpoints require header:

`X-Report-Vendor-Key: <ReportEvidence:VendorApiKey>`

## Config (reference)

```json
"ReportEvidence": {
  "VendorApiKey": "<long random secret>",
  "AutoEscalateNonCsamToVendor": true,
  "VendorNotifyUrl": "https://your-contractor.example/hooks/liberation-fleet-report"
}
```

- `AutoEscalateNonCsamToVendor`: non-CSAM reports are created as `EscalatedToVendor` so they show in `/ops` for contractor review. CSAM still goes to `QueuedForNcmec`.
- `VendorNotifyUrl`: optional HTTPS endpoint. On each new report, the server POSTs **metadata only** (no evidence ciphertext). Your contractor then calls `/ops?includeEvidence=true` when needed.

### Notify payload example

```json
{
  "reportId": 123,
  "reason": "Harassment",
  "status": "EscalatedToVendor",
  "createdAt": "2026-07-16T15:00:00Z",
  "targetType": "ChatMessage",
  "targetResourceId": 55,
  "targetAuthorUserId": 99
}
```

## Endpoints (reference)

| Call | Purpose |
|------|---------|
| `GET /api/reports/ops?limit=50` | List open reports (metadata) |
| `GET /api/reports/ops?includeEvidence=true` | List with decrypted evidence (logged access) |
| `POST /api/reports/vendor/webhook` | Apply triage label |

### Webhook body

```json
{
  "reportId": 123,
  "label": "csam",
  "notes": "Confirmed apparent CSAM — file CyberTipline"
}
```

### Labels

| Label | Effect |
|-------|--------|
| `csam`, `child_sexual_exploitation`, `csea` | Queue for NCMEC, quarantine content, freeze author |
| `ncii`, `violence`, `other` | Actioned + quarantine content |
| `none`, `benign`, `closed` | Close report |
| anything else | Mark EscalatedToVendor |

## Recommended workflow (Phase A → light B)

1. Keep `AutoEscalateNonCsamToVendor=false` at zero volume; you handle harassment with Block-only UX and personally handle `QueuedForNcmec`.
2. When volume grows, set `AutoEscalateNonCsamToVendor=true` and give a contractor `VendorApiKey`.
3. Contractor polls `/api/reports/ops` (or receives `VendorNotifyUrl` pings) every N minutes.
4. For non-obvious cases they fetch evidence once via `includeEvidence=true`.
5. They POST webhook with a label.
6. You only personally handle NCMEC portal filing for `QueuedForNcmec` items (see [NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md)) until you automate the CyberTipline API.
