# Safety reporting — engineering index

Bare-minimum illegal-content / abuse reporting for Liberation Fleet (US-first, 18+, E2EE preserved).

**Not legal advice.** Confirm assumptions in [JURISDICTION-ASSUMPTIONS.md](./JURISDICTION-ASSUMPTIONS.md) with counsel before production.

## What ships

| Layer | Location |
|-------|----------|
| Jurisdiction assumptions | `docs/JURISDICTION-ASSUMPTIONS.md` |
| Acceptable Use / Community Standards | `liberationfleet.client/src/assets/community-standards.txt` |
| Privacy (report packets + retention) | `liberationfleet.client/src/assets/privacy-policy.txt` §2.11 |
| Terms (enforcement / NCMEC) | `liberationfleet.client/src/assets/terms-of-use.txt` |
| ContentReport API + freeze/quarantine | `POST /api/reports`, `CreateContentReportCommand` |
| E2EE report evidence (AES-GCM server key) | `ReportEvidenceProtector` + `ReportEvidence` config |
| In-app Report UI | `report-content-dialog` on chat, fleet chat, forums, proposals, DMs, crewmate profile |
| NCMEC filing runbook | `docs/NCMEC-CSAM-runbook.md` |
| Vendor / contractor triage | `docs/REPORT-VENDOR-WEBHOOK.md`, `POST /api/reports/vendor/webhook`, `GET /api/reports/ops` |
| Non-CSAM evidence retention purge | `ContentReportRetentionHostedService` |

## User flow

1. User taps **Report** on UGC (after unlocking encryption when content is encrypted).
2. Client builds a one-time plaintext **evidence snapshot** (truncated text + media resource ids + attestation).
3. Server seals the snapshot with the **report-evidence key** (not crew/fleet keys).
4. Reason `ChildSexualExploitation` → status `QueuedForNcmec`, quarantine target, freeze author.
5. Reason `NonConsensualIntimateImage` → quarantine target (TAKE IT DOWN–style hide).
6. Other reasons → `Received` (optionally auto-escalated to vendor when configured).
7. Ops/vendor lists open reports; CSAM queue is filed to CyberTipline per the NCMEC runbook.

## Production secrets

```json
"ReportEvidence": {
  "AesKeyBase64": "<32-byte key, base64>",
  "VendorApiKey": "<long random secret>",
  "NonCsamRetentionDays": 90,
  "AutoEscalateNonCsamToVendor": false,
  "VendorNotifyUrl": ""
}
```

See [NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md) and [REPORT-VENDOR-WEBHOOK.md](./REPORT-VENDOR-WEBHOOK.md).
