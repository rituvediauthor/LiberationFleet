# App Review demo seed

Creates a **private** fleet (**Harbor Mutual Aid**) with two **private** crews (**Cedar Circle**, **River House**), nine demo accounts, E2EE identity keys (recovery phrases), and showcase data:

- Encrypted crew chat
- Verified gift-log entries (with encrypted notes)
- Library of Things offerings (durable / consumable / service) + quests
- An open proposal to vote on
- Fleet invite so both crews share Harbor Mutual Aid

## Run against production (or staging)

```powershell
cd tools/seed-review-demo
$env:LF_API_BASE = "https://liberationfleet.org"   # or your API origin
node seed.mjs
```

Requires **Node 20+** (Web Crypto). No npm install needed.

Re-running is mostly idempotent for accounts (login if already registered). If a user is already in a *different* crew, that user is skipped.

Output: `CREDENTIALS.local.md` (gitignored) with emails, the shared password, and every recovery phrase.

## Primary App Review login

| Field | Value |
|-------|--------|
| Email | `maya.chen@lf-review.test` |
| Password | `ReviewDemo!Harbor26` |
| Recovery phrase | `abandon ability able about above absent absorb abstract absurd abuse access accident` |

After sign-in, unlock encryption with the phrase (or remember on device), then open Cedar Circle → chat, gift log, Library of Things, proposals, and Harbor Mutual Aid.

Full account table: see [`CREDENTIALS.md`](./CREDENTIALS.md).

## Notes

- Demo-only. Do not use for real mutual aid.
- Turn **MFA off** on these accounts.
- Apple Notes should point at Maya’s email/password and mention unlocking with the recovery phrase once.
