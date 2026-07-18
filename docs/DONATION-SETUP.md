# Donation campaign — founder setup (step-by-step)

The app ships with donation UI and Stripe Checkout. **Card numbers never touch Liberation Fleet servers.** Complete these steps to make `/app/donate` accept money and record yearly totals.

Related: [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) (Key Vault + public URL), [LAUNCH-CHECKLIST.md](./LAUNCH-CHECKLIST.md).

---

## Step 1 — Create and activate Stripe

1. Open [https://dashboard.stripe.com/register](https://dashboard.stripe.com/register) and create an account.
2. Complete **business / identity / tax** onboarding (required for payouts).
3. **Settings → Bank accounts and scheduling** → add the account that should receive payouts.
4. Decide entity type (individual vs nonprofit). This build uses standard Checkout; nonprofit has extra Stripe settings outside the app.
5. Stay in **Test mode** (toggle in the Dashboard) until you have verified the webhook end-to-end.

---

## Step 2 — API keys

1. Dashboard → **Developers → API keys**.
2. Copy the **Secret key**:
   - Test: `sk_test_…`
   - Live (later): `sk_live_…`
3. Store it only in user secrets / Key Vault — **never commit**.

### Local development

```powershell
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project LiberationFleet.Server
dotnet user-secrets set "Stripe:PublicAppBaseUrl" "https://localhost:49236" --project LiberationFleet.Server
```

Use the HTTPS origin your Angular app actually opens (match SPA proxy / launch URL).

### Azure staging / production

1. Portal → your environment’s **Key Vault** → Secrets.
2. Open `Stripe-SecretKey` → **New version** → paste `sk_test_…` or `sk_live_…` → Create.
3. App Service → Configuration → set (or confirm):
   - `Stripe__PublicAppBaseUrl` = `https://your-host` (**no trailing slash**)
4. Restart the Web App.

Checkout stays disabled while `SecretKey` is missing or contains `change-me`.

---

## Step 3 — Webhook (required for profile donation totals)

Without this, Stripe can charge successfully but **app donation totals stay $0**.

### 3.1 Production / staging endpoint

1. Dashboard → **Developers → Webhooks → Add endpoint**.
2. **Endpoint URL:**
   ```
   https://YOUR_DOMAIN/api/donations/stripe/webhook
   ```
   Use the same host as `Stripe__PublicAppBaseUrl`.
3. **Events to send** (select manually):
   - `checkout.session.completed`
   - `checkout.session.async_payment_succeeded` (recommended)
4. **Add endpoint**.
5. Open the endpoint → **Signing secret** → Reveal → copy `whsec_…`.
6. Store it:
   - Local:  
     `dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..." --project LiberationFleet.Server`
   - Azure: Key Vault secret `Stripe-WebhookSecret` → New version → paste → Restart Web App.

### 3.2 Local forwarding (Stripe CLI)

1. Install [Stripe CLI](https://stripe.com/docs/stripe-cli).
2. `stripe login`
3. Forward to your local API (adjust port):
   ```bash
   stripe listen --forward-to https://localhost:YOUR_API_PORT/api/donations/stripe/webhook
   ```
4. CLI prints a `whsec_…` — put that in local user secrets as `Stripe:WebhookSecret`.

### 3.3 Test the webhook

1. Dashboard → Webhooks → your endpoint → **Send test webhook** → `checkout.session.completed`, **or** complete a real test Checkout with card `4242 4242 4242 4242`.
2. Confirm the endpoint shows success (2xx) and the user’s profile donation total updates after a completed session.

---

## Step 4 — Database

Donations need migration `AddAppDonations`.

- **Azure App Service:** EF migrations run on startup (default go-live path). Confirm the app starts cleanly after deploy.
- **Local:**
  ```bash
  dotnet ef database update --project LiberationFleet.Server
  ```

---

## Step 5 — Optional Stripe polish

- [ ] Enable customer emails / receipts in Stripe (Stripe sends them; you do not store cards).
- [ ] Set a **statement descriptor** that matches your brand.
- [ ] Add a short refund note to Privacy Policy / Community Standards.
- [ ] Tax-deductibility claims: talk to an accountant / 501(c) counsel first — this app does **not** issue formal tax receipts.

---

## Step 6 — Product behavior (already in code)

- Campaign widget sits **above** **Next aid** on crew home and fleet home.
- Audience rules:
  - Hidden when `EmergencyLevel > 0`
  - Outside Dec 20–Jan 3 UTC: contributors only; every **30** days if not in need, every **60** if in need
  - Dec 20–Jan 3 UTC: everyone not in emergency (once per high-season window)
- Donate page presets: $5 / $10 / $25 / $50 / $100 + custom whole dollars
- Your user **profile** shows app donation totals for previous + current calendar year

---

## Step 7 — Go-live switch to live keys

1. Stripe Dashboard → turn **off** Test mode.
2. Copy **live** Secret key → Key Vault `Stripe-SecretKey`.
3. Create a **live** webhook endpoint to the production URL (Step 3) → new `whsec_…` → Key Vault `Stripe-WebhookSecret`.
4. Confirm `Stripe__PublicAppBaseUrl` is the production HTTPS origin.
5. Restart Web App → run one small real donation to yourself if appropriate → verify profile totals.

---

## Not required for day one

- [ ] PayPal Giving / Donorbox
- [ ] Recurring subscriptions (Stripe Billing)
- [ ] Nonprofit processor switching

Peer-to-peer crew gifts use PayPal/Venmo/etc. **handles only** (no Stripe API).
