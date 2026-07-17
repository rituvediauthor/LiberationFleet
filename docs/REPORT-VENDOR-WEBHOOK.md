# Report triage webhook (vendor / contractor)

Use this when you outsource review and do not want to inspect every report yourself.

## Auth

All ops/vendor endpoints require header:

`X-Report-Vendor-Key: <ReportEvidence:VendorApiKey>`

## Config

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

## List open reports (metadata only)

`GET /api/reports/ops?limit=50`

## List with decrypted evidence (logs access)

`GET /api/reports/ops?includeEvidence=true`

## Apply triage label

`POST /api/reports/vendor/webhook`

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

1. Keep `AutoEscalateNonCsamToVendor=false` at zero volume; you ack harassment with Block-only UX and personally handle `QueuedForNcmec`.
2. When volume grows, set `AutoEscalateNonCsamToVendor=true` and give a contractor `VendorApiKey`.
3. Contractor polls `/api/reports/ops` (or receives `VendorNotifyUrl` pings) every N minutes.
4. For non-obvious cases they fetch evidence once via `includeEvidence=true`.
5. They POST webhook with a label.
6. You only personally handle NCMEC portal filing for `QueuedForNcmec` items (see `NCMEC-CSAM-runbook.md`) until you automate the CyberTipline API.
