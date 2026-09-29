# Report triage (vendor / contractor) — step-by-step

This is **not** a Stripe- or LiveKit-style SaaS signup. Liberation Fleet does **not** call a third-party moderation product. Instead, **you** (or a person you hire) call **your** API with a shared secret to list reports and apply labels.

Related: [SAFETY-REPORTING.md](./SAFETY-REPORTING.md), [NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md), [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md).

**Not legal advice.** CSAM / child-safety review has legal and wellness constraints — confirm with counsel before giving anyone access to evidence.

---

## What you are configuring

| Piece | Where it lives | Purpose |
|-------|----------------|---------|
| `ReportEvidence-VendorApiKey` | Key Vault (each environment) | Shared secret for header `X-Report-Vendor-Key` |
| `ReportEvidence__AutoEscalateNonCsamToVendor` | App Service env var (optional) | When `true`, non-CSAM reports start as `EscalatedToVendor` for the reviewer queue |
| `ReportEvidence__VendorNotifyUrl` | App Service env var (optional) | HTTPS URL the API POSTs **metadata-only** pings to on new reports |

App Service may also show `ReportEvidence__VendorApiKey` as an `@Microsoft.KeyVault(...)` reference — same pattern as Stripe. Put the real secret **only** in Key Vault.

---

## Choose a path (pick one)

### Path A — Free / day one (recommended until volume grows)

**You** are the reviewer. No external company required. There is **no founder “Reports” page in the Angular app yet** — you use the ops API (PowerShell / curl) with your vendor key.

1. Generate a vendor API key (Step 1 below).
2. Store it in **production** Key Vault (Step 2).
3. Leave `ReportEvidence__AutoEscalateNonCsamToVendor` **unset or `false`**.
4. Follow **Founder day-to-day** below (and [NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md) for CSAM).

Skip Path B until you actually need help.

#### Founder day-to-day (how you handle reports yourself)

**What the app already does for you**

| User report reason | Automatic server action | Your job |
|--------------------|-------------------------|----------|
| Child sexual exploitation | Status `QueuedForNcmec`; content quarantined; author account frozen | File CyberTipline ASAP ([NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md)), then close when appropriate |
| Non-consensual intimate image | Content quarantined; status `Received` | Review evidence; label `ncii` (or `closed` if false positive) |
| Harassment / spam / threats / other | Status `Received` only (reporter can also Block) | Triage when you can; close benign; action serious cases |

**Setup once**

1. Complete Step 1–2 (key in `lfleetproductionkv`).
2. Register as an ESP with NCMEC if you have not ([NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md) §1) — required before you can file CSAM reports properly.
3. Keep the vendor key in your password manager.

**Cadence**

- **Daily (or every few days while traffic is low):** list open reports (metadata only).
- **Immediately if you see `QueuedForNcmec`:** open evidence and file CyberTipline the same day you become aware.

**List open reports (metadata — safe default)**

```powershell
$base = "https://liberationfleet.org"   # your production origin
$key  = "<your-vendor-api-key>"

Invoke-RestMethod -Method GET -Uri "$base/api/reports/ops?limit=50" -Headers @{
  "X-Report-Vendor-Key" = $key
} | ConvertTo-Json -Depth 6
```

Look at each item’s `status` / `reason`. Prioritize `QueuedForNcmec`.

**Open evidence only when you need it** (access is logged)

```powershell
Invoke-RestMethod -Method GET -Uri "$base/api/reports/ops?includeEvidence=true&limit=20" -Headers @{
  "X-Report-Vendor-Key" = $key
} | ConvertTo-Json -Depth 8
```

**Apply a decision** (replace `123` with the real `reportId`)

```powershell
# Close as benign
$body = @{ reportId = 123; label = "none"; notes = "Reviewed — no violation" } | ConvertTo-Json

# Or quarantine-style action for violence / other illegal (see labels table)
# $body = @{ reportId = 123; label = "violence"; notes = "Threats — quarantined" } | ConvertTo-Json

# Or after NCMEC filing / follow-up close
# $body = @{ reportId = 123; label = "closed"; notes = "CyberTipline filed YYYY-MM-DD" } | ConvertTo-Json

Invoke-RestMethod -Method POST -Uri "$base/api/reports/vendor/webhook" -Headers @{
  "X-Report-Vendor-Key" = $key
  "Content-Type" = "application/json"
} -Body $body
```

**Practical tips**

- User **Block** already handles a lot of harassment without you intervening.
- You do **not** need a paid vendor to launch.
- Do not enable `AutoEscalateNonCsamToVendor` until someone else is actually reviewing the queue.
- False-positive CSAM freeze: with counsel approval, set that user’s `IsActive = true` in the database (see NCMEC runbook §4).

---

### Path B — Affordable human contractor (later)

Hire a **vetted Trust & Safety / content-moderation contractor** (person or small firm) who will:

- Poll `GET /api/reports/ops` (or receive your optional notify pings)
- Review **non-CSAM** queue when you enable auto-escalate
- POST labels to `POST /api/reports/vendor/webhook`

**You** still own NCMEC CyberTipline filing for CSAM (`QueuedForNcmec`) until that is automated.

#### Where to look (realistic for a small nonprofit)

| Option | Cost | Notes |
|--------|------|--------|
| **You / co-founder / board volunteer** | Free | Best fit until traffic is real. Use Path A. |
| **Part-time T&S freelancer** with prior platform moderation experience | Often roughly **$25–75/hr** (varies) | Search [Upwork](https://www.upwork.com/) for “trust and safety” / “content moderator” / “community safety”. Require NDA, background check if possible, and **do not** send them CSAM evidence unless counsel approves a formal process. Prefer they only see non-CSAM labels. |
| **Boutique T&S consultancies** | Quote-based (usually not “free tier”) | Ask for a small retainer (e.g. X hours/month) once volume justifies it. |
| **Large BPOs** (e.g. [TaskUs Trust & Safety](https://www.taskus.com/services/trust-and-safety/protect-your-platform-with-taskus-trust-safety-solutions/)) | Enterprise pricing | Overkill and usually unaffordable at launch. |

**Do not** use random gig workers for potential CSAM / sexual-abuse material review. That is a wellness and legal hazard. Keep CSAM in `QueuedForNcmec` for your ESP / compliance contact.

#### AI moderation products (Hive, OpenAI Moderation, ActiveFence, etc.)

Those products have **their own** APIs. They do **not** plug into this document’s endpoints without new engineering. This guide only covers Liberation Fleet’s contractor API.

---

## Step 1 — Generate a vendor API key

1. Open PowerShell and run:

   ```powershell
   $bytes = New-Object byte[] 48
   [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
   [Convert]::ToBase64String($bytes)
   ```

2. Copy the output (long Base64 string).
3. Store it in your password manager as e.g. `LiberationFleet production ReportEvidence-VendorApiKey`.
4. If you hire a contractor later, share that value **only** over a secure channel (1Password / Bitwarden send link, not email plaintext). Generate a **different** key for staging.

---

## Step 2 — Put the key in Azure (production)

Do this after [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) Step 11 (Key Vault `lfleetproductionkv` exists).

### 2.1 Key Vault secret

1. Open [portal.azure.com](https://portal.azure.com) → subscription that hosts production (**LF_sub** if that is yours).
2. Resource group **`rg-lfleet-production`** → Key Vault **`lfleetproductionkv`**.
3. Left menu → **Objects** → **Secrets** (or **Secrets**).
4. Open **`ReportEvidence-VendorApiKey`**.
5. **New version** → paste the Base64 secret from Step 1 → **Create**.
6. Leave the App Service setting `ReportEvidence__VendorApiKey` as the `@Microsoft.KeyVault(...)` reference Terraform created. Do **not** paste the raw secret into Environment variables.

### 2.2 Restart the Web App

1. Portal → **`app-lfleet-production`** → Overview → **Restart**.

### 2.3 Staging (only if staging is up)

Repeat in **`lfleetstagingkv`** with a **different** random key. Restart **`app-lfleet-staging`**.

### 2.4 Optional App Service flags (skip on day one)

Only when you have a real contractor ready for non-CSAM queue:

1. Portal → **`app-lfleet-production`** → **Settings** → **Environment variables**.
2. **+ Add** or edit:

| Name | Value | When |
|------|--------|------|
| `ReportEvidence__AutoEscalateNonCsamToVendor` | `true` | Contractor will poll `/ops` for non-CSAM |
| `ReportEvidence__VendorNotifyUrl` | `https://contractor.example/hooks/...` | Only if they give you an HTTPS ping URL |

3. **Apply** / **Save** → **Restart**.

---

## Step 3 — What to give a contractor (Path B only)

Send them:

1. **API base URL** — same origin users use, e.g. `https://liberationfleet.org` (no trailing slash).
2. **Auth header** — every request must include:
   ```http
   X-Report-Vendor-Key: <the-secret-from-Step-1>
   ```
3. This document (endpoints + labels table below).
4. Your Community Standards / escalation rules in plain language.
5. Expectation: they POST labels; **you** still file NCMEC for CSAM until CyberTipline is automated ([NCMEC-CSAM-runbook.md](./NCMEC-CSAM-runbook.md)).

---

## Step 4 — Smoke test (you can do this yourself — Path A)

Replace the base URL and key. PowerShell example against production:

```powershell
$base = "https://liberationfleet.org"   # or https://app-lfleet-production.azurewebsites.net
$key  = "<paste-vendor-api-key>"

Invoke-RestMethod -Method GET -Uri "$base/api/reports/ops?limit=50" -Headers @{
  "X-Report-Vendor-Key" = $key
}
```

With evidence (logged access — use sparingly):

```powershell
Invoke-RestMethod -Method GET -Uri "$base/api/reports/ops?includeEvidence=true&limit=10" -Headers @{
  "X-Report-Vendor-Key" = $key
}
```

Apply a benign label to a **test** report id:

```powershell
$body = @{ reportId = 123; label = "none"; notes = "Benign smoke test" } | ConvertTo-Json
Invoke-RestMethod -Method POST -Uri "$base/api/reports/vendor/webhook" -Headers @{
  "X-Report-Vendor-Key" = $key
  "Content-Type" = "application/json"
} -Body $body
```

Expected: `401`/`403` if the key is wrong; JSON list or success if the key and report id are valid.

---

## Auth (reference)

All ops/vendor endpoints require:

```http
X-Report-Vendor-Key: <ReportEvidence:VendorApiKey>
```

## Config (reference)

```json
"ReportEvidence": {
  "VendorApiKey": "<long random secret>",
  "AutoEscalateNonCsamToVendor": false,
  "VendorNotifyUrl": ""
}
```

| Setting | Day-one recommendation |
|---------|------------------------|
| `VendorApiKey` | Set in Key Vault so **you** can use `/ops` |
| `AutoEscalateNonCsamToVendor` | `false` until a contractor exists |
| `VendorNotifyUrl` | Empty until a contractor gives you a webhook |

### Notify payload example (metadata only — no evidence ciphertext)

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

## Recommended workflow

1. **Launch:** Path A — generate key, Key Vault only, auto-escalate **off**, you handle CSAM via NCMEC runbook.
2. **Volume grows:** Path B — hire a vetted non-CSAM reviewer, share key + base URL, set `AutoEscalateNonCsamToVendor=true`.
3. Contractor polls `/api/reports/ops` (or notify URL) on a schedule.
4. They POST a label; you only personally handle `QueuedForNcmec` CyberTipline filing until automation exists.
