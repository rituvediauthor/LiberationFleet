# Report triage webhook (vendor / contractor)

Use this when you outsource review and do not want to inspect every report yourself.

## Auth

All ops/vendor endpoints require header:

`X-Report-Vendor-Key: <ReportEvidence:VendorApiKey>`

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

## Recommended workflow

1. Contractor polls `/api/reports/ops` every N minutes (or you push IDs on `QueuedForNcmec` / `Received`).
2. For non-obvious cases they fetch evidence once.
3. They POST webhook with a label.
4. You only personally handle NCMEC portal filing for `QueuedForNcmec` items (see `NCMEC-CSAM-runbook.md`) until you automate the CyberTipline API.
