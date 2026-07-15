# Donation campaign — founder setup checklist

The app ships with donation UI and Stripe Checkout wiring. **Card numbers never touch Liberation Fleet servers.** Complete these account/config steps to make `/app/donate` accept real money.

## 1. Stripe account
- [ ] Create a Stripe account at https://dashboard.stripe.com/register
- [ ] Complete business identity / tax onboarding (required to receive payouts)
- [ ] Confirm payouts go to your bank account
- [ ] Decide: individual vs nonprofit (nonprofit has different Stripe settings; this build uses standard Checkout)

## 2. Stripe API keys
- [ ] Dashboard → Developers → API keys
- [ ] Copy **Secret key** (`sk_test_...` while testing, then `sk_live_...`)
- [ ] Store only in user secrets / env / Key Vault — never commit live keys

Local example:

```powershell
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project LiberationFleet.Server
dotnet user-secrets set "Stripe:PublicAppBaseUrl" "https://localhost:49236" --project LiberationFleet.Server
```

Production `appsettings` / env:
- `Stripe__SecretKey`
- `Stripe__PublicAppBaseUrl` (your real HTTPS app origin, no trailing slash)
- `Stripe__WebhookSecret`

Checkout is disabled until `SecretKey` is set and does not contain `change-me`.

## 3. Stripe webhook (records completed donations for tax-year totals)
- [ ] Dashboard → Developers → Webhooks → Add endpoint
- [ ] URL: `https://YOUR_DOMAIN/api/donations/stripe/webhook`
- [ ] Events to send:
  - `checkout.session.completed`
  - `checkout.session.async_payment_succeeded` (optional but recommended)
- [ ] Copy signing secret (`whsec_...`) into `Stripe:WebhookSecret`
- [ ] For local dev, use Stripe CLI:  
  `stripe listen --forward-to https://localhost:YOUR_PORT/api/donations/stripe/webhook`

Without the webhook, payments can succeed in Stripe but **profile donation totals will stay $0**.

## 4. Database
- [ ] Apply migration `AddAppDonations` (`dotnet ef database update` from the Server project)

## 5. Optional Stripe polish (single-person friendly)
- [ ] Enable Customer emails / receipts in Stripe (Stripe sends receipts; you do not store cards)
- [ ] Add a Statement descriptor that matches your brand
- [ ] Create a simple Refund policy note for your Privacy Policy / Community Standards
- [ ] If you want tax-deductibility claims, talk to an accountant / 501(c) counsel first — this app does **not** issue formal tax receipts

## 6. Product behavior already in code
- Campaign widget sits **above** **Next aid** on crew home (crew copy) and fleet home (fleet copy)
- Audience:
  - Never shown when `EmergencyLevel > 0`
  - Outside Dec 20–Jan 3: contributors only; every **30** days if not in need, every **60** if in need
  - Dec 20–Jan 3 UTC: everyone not in emergency (once per high-season window)
- Donate page presets: $5 / $10 / $25 / $50 / $100 + custom whole dollars
- Profile (your user profile, not crewmate detail) shows app donation totals for previous + current calendar year

## 7. Not required for day one
- [ ] PayPal Giving / Donorbox (you can add later; Stripe Checkout is enough for a solo operator)
- [ ] Recurring subscriptions (monthly donations) — add Stripe Billing later if needed
- [ ] Nonprofit payment processor switching
