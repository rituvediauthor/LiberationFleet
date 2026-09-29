# NCMEC CyberTipline — CSAM filing runbook

**Status:** Ready for ESP registration. Manual filing is the Phase-1 path.  
**Not legal advice.** Confirm with counsel before production.

## 1. Register as an ESP (company / provider registration)

**Important:** The public “Make a CyberTipline Report” page is for **individuals**. It is **not** where companies register as an Electronic Service Provider (ESP).

Liberation Fleet needs **ESP registration** so *you* (the provider) can submit provider reports when you have actual knowledge of apparent CSAM.

### 1.1 Where to register

1. Open the ESP registration portal and register:  
   **[https://esp.ncmec.org/registration](https://esp.ncmec.org/registration)**

### 1.2 What to prepare before / during the form

Have this ready (wording varies; use your real legal entity):

| Item | Example / notes |
|------|-----------------|
| Legal entity name | Your LLC / nonprofit name |
| Product / service description | Encrypted group chat / mutual-aid community app for adults (18+) |
| Website | `https://liberationfleet.org` (or current production URL) |
| Compliance / reporting contact | Your name, email, phone |
| What you might report | User reports of apparent CSAM / online enticement; sealed evidence snapshots; user ids; timestamps |
| Service type | Online messaging / social / UGC hosting (pick the closest options on the form) |

### 1.3 After you submit

1. Wait for NCMEC to determine eligibility and send next steps (credentials / portal access). This is **not** instant.
2. Designate a compliance contact (you, unless counsel advises otherwise).
3. Store any ESP username/password **offline** (password manager) — never commit to git.
4. Phase-1 filing is usually the **provider web form** NCMEC gives you after approval. The automated API (`https://report.cybertip.org/ispws`) is optional later and also requires credentials from NCMEC ([API docs](https://report.cybertip.org/ispws/documentation/index.html)).

### 1.4 Until ESP registration is approved

- Keep production report evidence preserved (`QueuedForNcmec` is never auto-purged by the retention job).
- Do not ignore CSAM-category reports — escalate with counsel on how to file in the interim.
- Public CyberTipline reports (as a private person) are a different channel; ask counsel before relying on that instead of ESP reporting.

---

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

- [ ] ESP registration submitted at [https://esp.ncmec.org/registration](https://esp.ncmec.org/registration) (or email sent to ESPteam@ncmec.org)
- [ ] Compliance contact named
- [ ] Production `AesKeyBase64` + `VendorApiKey` stored in secret manager (not git)
- [ ] First dry-run filing using a synthetic / test report in a non-production environment (if NCMEC provides a test path) or tabletop walkthrough of this runbook

Until registration is approved, do **not** process live CSAM-category reports in production beyond acknowledging and preserving evidence offline with counsel.