# Donation campaign — founder setup (step-by-step)

The app ships with donation UI and Stripe Checkout. **Card numbers never touch Liberation Fleet servers.**

Related: [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) (Key Vault + public URL), [LAUNCH-CHECKLIST.md](./LAUNCH-CHECKLIST.md).

### Which environment am I configuring?

| Environment | Stripe mode | Where secrets live | When |
|-------------|-------------|--------------------|------|
| **Local** | **Test** (`sk_test_…`) | .NET user-secrets | Anytime while developing |
| **Azure staging** | **Test** (`sk_test_…`) | Staging Key Vault (e.g. `lfleetstagingkv`) | After [AZURE-GO-LIVE](./AZURE-GO-LIVE.md) Steps 6–9 |
| **Azure production** | **Live** (`sk_live_…`) | Production Key Vault (e.g. `lfleetproductionkv`) | After [AZURE-GO-LIVE](./AZURE-GO-LIVE.md) Step 11 |

Liberation Fleet “sandbox” / Docker is **not** Stripe Test mode. You must use Stripe’s Test toggle and `sk_test_…` keys until production go-live.

Do **not** put live keys in staging or user-secrets. Do **not** put test keys in production.

---

## Part A — Stripe account (once)

1. Open [https://dashboard.stripe.com/register](https://dashboard.stripe.com/register) and create an account.
2. Complete **business / identity / tax** onboarding (required for payouts).
3. **Settings → Bank accounts and scheduling** → add the **nonprofit org** bank account that should receive payouts.
4. Entity type: **nonprofit / company** with legal name + EIN (not individual), once the org exists.
5. Keep the Dashboard **Test mode** toggle **on** until Part D (production).

Optional polish (any time):

- [ ] Customer emails / receipts in Stripe
- [ ] Statement descriptor matching your brand
- [ ] Refund note in Privacy Policy / Community Standards
- [ ] Tax-deductibility claims: accountant / 501(c) counsel first — this app does **not** issue formal tax receipts

---

## Part B — Local development (Test mode only)

Checkout is disabled until the API sees a real `sk_test_…` key (not `change-me`).

**If you use Docker Compose** (`docker compose up`), put Stripe in the repo-root **`.env`** file — `dotnet user-secrets` only apply to `dotnet run`, not containers.

```env
STRIPE_SECRET_KEY=sk_test_...
STRIPE_WEBHOOK_SECRET=whsec_...
STRIPE_PUBLIC_APP_BASE_URL=http://localhost:8080
```

Then recreate the API container:

```powershell
docker compose up -d --force-recreate api
```

Webhook forward for Docker:

```powershell
stripe listen --forward-to http://localhost:8080/api/donations/stripe/webhook
```

**If you use `dotnet run`** (not Docker), use user-secrets instead:

```powershell
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project ".\LiberationFleet.Server\LiberationFleet.Server.csproj"
dotnet user-secrets set "Stripe:PublicAppBaseUrl" "https://localhost:49236" --project ".\LiberationFleet.Server\LiberationFleet.Server.csproj"
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..." --project ".\LiberationFleet.Server\LiberationFleet.Server.csproj"
```

### B.1 Database (dotnet run only)

```bash
dotnet ef database update --project LiberationFleet.Server
```

(Docker runs EF migrations on startup.)

### B.2 Verify locally

1. Confirm the donate page no longer says “Donations are being set up…”
2. Complete a test Checkout with card `4242 4242 4242 4242`.
3. Confirm profile donation totals update (requires webhook + matching `WebhookSecret`).

---

## Part C — Azure staging (Test mode only)

Prerequisites: staging App Service + Key Vault exist ([AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) Steps 6–7). Terraform creates Key Vault name `{project}{environment}kv` → typically **`lfleetstagingkv`**.

### C.1 Keys in staging Key Vault

1. Stripe Dashboard → **Test mode ON** → Developers → API keys → copy `sk_test_…`.
2. Portal → **`lfleetstagingkv`** (or `terraform output key_vault_name` for staging) → Secrets.
3. `Stripe-SecretKey` → **New version** → paste `sk_test_…` → Create.
4. App Service (staging) → Configuration → confirm:
   - `Stripe__PublicAppBaseUrl` = staging HTTPS URL (**no trailing slash**)  
     (same host as `terraform output app_public_url`)
5. Restart the staging Web App.

### C.2 Staging webhook (Event destination)

Stripe’s UI now says **Add destination** (formerly “Add endpoint”).

1. Stripe Dashboard → **Test mode ON** → **Developers** → **Webhooks** (Event destinations) → **Add destination**.
2. **Event destination scope:** choose **Your account** (not Connected accounts — this app does not use Stripe Connect).
3. **API version:** leave the account default.
4. **Events to send** (select manually):
   - `checkout.session.completed`
   - `checkout.session.async_payment_succeeded` (recommended)
5. **Continue**.
6. Destination type: **Webhook endpoint** (or equivalent).
7. **Endpoint URL** (must match `Stripe__PublicAppBaseUrl` + path; no trailing slash on the host):
   ```
   https://app-lfleet-staging.azurewebsites.net/api/donations/stripe/webhook
   ```
   Or substitute your current staging host from `terraform output app_public_url`.
8. Create the destination → open it → **Signing secret** → Reveal → copy `whsec_…`.
9. Staging Key Vault → `Stripe-WebhookSecret` → New version → paste → Restart staging Web App.

Use a **separate** destination for staging vs production. Do not reuse a production `whsec_…` on staging.

### C.3 Verify staging

1. Open staging `/app/donate` → you should see a staging warning that donations will not work there.
   For Stripe test-key verification on a non-production host that still has Checkout configured, use test card `4242…`.
2. Destination / webhook delivery shows 2xx; profile totals update.
3. EF migrations run on App Service startup — confirm the app starts cleanly after deploy.

---

## Part D — Azure production (Live mode only)

Prerequisites: production infra applied ([AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) **Step 11**). Key Vault is typically **`lfleetproductionkv`** (from `environment = "production"` in `production.tfvars` — **not** created during staging).

### D.1 Production Key Vault keys

1. Stripe Dashboard → turn **Test mode OFF** (Live).
2. Developers → API keys → copy `sk_live_…`.
3. Portal → **`lfleetproductionkv`** → Secrets → `Stripe-SecretKey` → New version → paste `sk_live_…`.
4. Production App Service → `Stripe__PublicAppBaseUrl` = production HTTPS origin (custom domain if you have one).
5. Restart production Web App.

### D.2 Production webhook (new Event destination)

1. Stripe Dashboard → **Live mode** → Developers → Webhooks → **Add destination** (do **not** edit the staging Test-mode destination).
2. Same choices as staging C.2: scope **Your account**, default API version, same two `checkout.session.*` events.
3. **Endpoint URL:**
   ```
   https://YOUR_PRODUCTION_HOST/api/donations/stripe/webhook
   ```
4. New live `whsec_…` → production Key Vault `Stripe-WebhookSecret` → Restart production.

### D.3 Verify production

1. One small real donation (to yourself if appropriate).
2. Confirm webhook/destination delivery 2xx and profile totals.
3. Confirm staging still uses **test** keys and its own destination.
---

## Product behavior (already in code)

- Campaign widget sits **above** **Next aid** on crew home and fleet home.
- Audience rules:
  - Hidden when `EmergencyLevel > 0`
  - Outside Dec 20–Jan 3 UTC: contributors only; every **30** days if not in need, every **60** if in need
  - Dec 20–Jan 3 UTC: everyone not in emergency (once per high-season window)
- Donate page presets: $5 / $10 / $25 / $50 / $100 + custom whole dollars
- Profile shows app donation totals for previous + current calendar year

---

## Not required for day one

- [ ] PayPal Giving / Donorbox
- [ ] Recurring subscriptions (Stripe Billing)
- [ ] Nonprofit processor switching

Peer-to-peer crew gifts use PayPal/Venmo/etc. **handles only** (no Stripe API).
