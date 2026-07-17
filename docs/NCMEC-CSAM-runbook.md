# NCMEC CyberTipline — CSAM filing runbook

**Status:** Ready for ESP registration. Manual filing is the Phase-1 path.  
**Not legal advice.** Confirm with counsel before production.

## 1. Register as an ESP

1. Go to [NCMEC CyberTipline / ESP registration](https://report.cybertip.org/) (NCMEC provider onboarding).
2. Designate a compliance contact (you or a contractor).
3. Store ESP credentials offline; never commit them to git.
4. After approval, use the provider portal (API later if volume grows).

## 2. When you have a duty to report

Under 18 U.S.C. § 2258A, when Liberation Fleet has **actual knowledge** of apparent CSAM, child sex trafficking, or online enticement of a minor, file a CyberTipline report.

In this product, actual knowledge typically arrives when:
- A user submits an in-app Report with reason `ChildSexualExploitation` (status `QueuedForNcmec`), or
- A moderation vendor webhook labels a report `csam` / `csea`.

**Do not** auto-POST every harassment report to NCMEC.

## 3. Filing checklist (manual)

1. Open ops list: `GET /api/reports/ops?includeEvidence=true` with header `X-Report-Vendor-Key: <VendorApiKey>`.
2. Filter `status == QueuedForNcmec`.
3. For each item, decrypt evidence is already returned when `includeEvidence=true`.
4. File at CyberTipline with (as available):
   - Incident description / reporter category
   - Involved user ids / usernames from evidence
   - Snapshot text and any media resource refs
   - Timestamps (`createdAt`, `escalatedToNcmecAt`)
   - App / URL / how the content was hosted
5. Log access is automatic via `ContentReportAccessLogs` when evidence is viewed.
6. After filing, set `OpsNotes` via vendor webhook `label=csam` (already applied) or close after LE follow-up using `label=closed` when appropriate.

## 4. Account freeze / quarantine (automated)

On CSAM-category create or vendor `csam` label, the server:
- Soft-deletes the reported content row when a content id is present (`IsDeleted = true`)
- Sets `User.IsActive = false` for the reported author (login blocked with a freeze message)

To manually unfreeze after false positive (with counsel approval): set `Users.IsActive = true` for that user id.

## 5. Retention

Configured by `ReportEvidence:NonCsamRetentionDays` (default 90).  
`ContentReportRetentionHostedService` clears sealed evidence for expired **non-CSAM** packets on a ~12h schedule and closes those reports.

CSAM / escalated packets (`QueuedForNcmec` or `EscalatedToNcmecAt` set): preserve until legal counsel says otherwise; do not purge opportunistically.

## 6. Production secrets

Set in environment / secret store:

```json
"ReportEvidence": {
  "AesKeyBase64": "<32-byte key, base64>",
  "VendorApiKey": "<long random secret>",
  "NonCsamRetentionDays": 90,
  "AutoEscalateNonCsamToVendor": false,
  "VendorNotifyUrl": ""
}
```

Generate AES key: `[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])`  
or use a proper CSPRNG.

## 7. Registration status (ops)

- [ ] ESP registration submitted at CyberTipline
- [ ] Compliance contact named
- [ ] Production `AesKeyBase64` + `VendorApiKey` stored in secret manager (not git)
- [ ] First dry-run filing using a synthetic / test report in a non-production environment (if NCMEC provides a test path) or tabletop walkthrough of this runbook

Until registration is approved, do **not** process live CSAM-category reports in production beyond acknowledging and preserving evidence offline with counsel.
