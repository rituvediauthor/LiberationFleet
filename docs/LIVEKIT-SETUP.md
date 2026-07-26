# LiveKit voice setup (step-by-step)

Crew voice uses **LiveKit** as the SFU and ASP.NET + SignalR for presence. The Angular client never receives the API secret — only short-lived tokens from your API.

Related: [VOICE-QA.md](./VOICE-QA.md), [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md).

---

## Path A — Local development (Docker)

No LiveKit Cloud account needed. Docker runs LiveKit; the API uses the keys already in this repo.

### A.1 Start infrastructure

From the repo root:

```bash
docker compose -f docker-compose.dev.yml up -d db livekit coturn
```

| Service | Endpoint / notes |
|---------|------------------|
| LiveKit WebSocket | `ws://localhost:7880` |
| Coturn TURN | UDP/TCP `3478` (host network), credentials `livekit` / `livekitturn` |
| Config file | `infrastructure/livekit.yaml` |

### A.2 Confirm the API already points at local LiveKit

**You usually do nothing here.** Local keys are already checked into the repo. This step is a quick sanity check before smoke-testing voice.

1. Open `LiberationFleet.Server/appsettings.json` and confirm a `LiveKit` section exists with values like:

```json
"LiveKit": {
  "Host": "ws://localhost:7880",
  "ApiKey": "devkey",
  "ApiSecret": "secretsecretsecretsecretsecretsecret12",
  "TokenTtlMinutes": 360
}
```

2. Open `infrastructure/livekit.yaml` and confirm the `keys:` entry matches that `ApiKey` / `ApiSecret` (default: `devkey` + the long secret above).
3. **If they already match → skip to A.3.** Do not copy anything from LiveKit Cloud for local Docker.
4. **Only edit** if you changed `livekit.yaml`, or if user secrets / `.env` override LiveKit and those overrides are wrong or missing. Env names (if you use them):

- `LiveKit__Host`
- `LiveKit__ApiKey`
- `LiveKit__ApiSecret`
- `LiveKit__TokenTtlMinutes`

Where values come from locally:

| Setting | Source |
|---------|--------|
| `Host` | Docker maps LiveKit to port `7880` → `ws://localhost:7880` |
| `ApiKey` / `ApiSecret` | You invent them for local Docker; they must match `infrastructure/livekit.yaml` (defaults already set) |
| `TokenTtlMinutes` | App-only; `360` is fine for local |

### A.3 Smoke test

1. Run the API (`dotnet run` in `LiberationFleet.Server`) and the Angular client; sign in; open a crew chat room.
2. Open `/app/crew/chats/:id/voice` (pass 18+ gate if shown).
3. Join — browser should request microphone; LiveKit connects; presence appears on the chat list.
4. Run through [VOICE-QA.md](./VOICE-QA.md).

If the browser shows `WebSocket …/rtc … 500`, check `docker logs liberationfleet-livekit-dev`. A bad `stun_servers` entry in `infrastructure/livekit.yaml` (for example `stun:stun.l.google.com:19302`) causes LiveKit 1.13+ to reject joins with “too many colons in address”. Use `stun.l.google.com:19302` (host:port only), then recreate LiveKit.

An `AudioContext was not allowed to start` console warning alone is usually harmless Chrome autoplay policy noise; fix the `/rtc` 500 first.

If join returns a token but the UI says microphone/WebRTC failed with no Network errors: LiveKit ICE is advertising an unreachable Docker bridge IP. Local `infrastructure/livekit.yaml` must set `node_ip: 127.0.0.1` and `enable_loopback_candidate: true`, then recreate LiveKit (`docker compose -f docker-compose.dev.yml up -d --force-recreate livekit`).

---

## Path B — Azure staging (LiveKit Cloud)

Do this after staging infra exists ([AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) Steps 6–7). Uses staging Key Vault (e.g. `lfleetstagingkv`).

### B.1 Create or reuse a LiveKit Cloud project

1. Go to [https://cloud.livekit.io](https://cloud.livekit.io) and sign up / sign in.
2. **Create project** (or use a dedicated staging project).
3. **Settings → Keys** — copy WebSocket URL (`wss://…`), API Key, API Secret.

### B.2 Wire staging

1. Edit `infrastructure/terraform/environments/staging.tfvars` and **uncomment** (or add) `livekit_host` with your Cloud URL. If that line stays commented, Terraform writes an **empty** `LiveKit__Host` and voice will not work:

   ```hcl
   livekit_host = "wss://xxxxx.livekit.cloud"
   ```

2. Apply **staging** only (from `infrastructure/terraform`; quote the path in PowerShell):

   ```powershell
   terraform init -backend-config="environments/staging.backend.hcl"
   terraform apply -var-file="environments/staging.tfvars"
   ```

   Enter `yes` when prompted. Without quotes, PowerShell can split the path so Terraform fails loading `.tfvars` as a plan file.

3. Portal → **staging** Key Vault → Secrets:
   - `LiveKit-ApiKey` → New version
   - `LiveKit-ApiSecret` → New version
4. Restart the **staging** Web App.
5. Confirm staging App Service → **Environment variables** → `LiveKit__Host` equals that same `wss://` URL (not blank).

### B.3 Verify on staging

1. Open the staging site → crew → voice channel.
2. Join from two browsers; confirm audio / presence.
3. If join fails: Key Vault values, `wss://` (not `ws://`), browser console / API logs.

---

## Path C — Azure production (LiveKit Cloud)

Do this only after [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md) **Step 11** (production Key Vault e.g. `lfleetproductionkv` exists). Prefer a **separate** LiveKit Cloud project from staging.

### C.1 Production project keys

1. LiveKit Cloud → production project → copy `wss://…`, API Key, API Secret.

### C.2 Wire production

1. Edit `infrastructure/terraform/environments/production.tfvars`:

   ```hcl
   livekit_host = "wss://xxxxx.livekit.cloud"
   ```

2. Apply **production** only (quote the path in PowerShell):

   ```powershell
   terraform init -reconfigure -backend-config="environments/production.backend.hcl"
   terraform apply -var-file="environments/production.tfvars"
   ```

3. Portal → **production** Key Vault → `LiveKit-ApiKey` / `LiveKit-ApiSecret` → New versions.
4. Restart the **production** Web App.

### C.3 Verify on production

Same checks as Path B.3 on the production URL.

---

## Path D — Self-hosted LiveKit + TURN (advanced)

Use this only if you cannot use LiveKit Cloud.

1. Provision a VM or Container Apps environment that allows **UDP** for WebRTC/TURN.
2. Deploy LiveKit Server + coturn (or equivalent) with public DNS and TLS (`wss://`).
3. Set strong API key/secret and TURN credentials; never ship secrets to the Angular client.
4. Wire `LiveKit__Host`, `LiveKit__ApiKey`, `LiveKit__ApiSecret` the same way as Path B (staging) or Path C (production).
5. Open firewall for TURN (typically UDP/TCP 3478 and media relay ports per your coturn config).

---

## API surface (reference)

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/chats/rooms/{id}/voice/join` | Mint LiveKit token + create session |
| POST | `/api/chats/rooms/{id}/voice/leave` | Leave + cleanup |
| GET | `/api/chats/voice/presence?crewId=` | Sidebar snapshot |
| POST | `/api/chats/rooms/{id}/voice/disconnect` | Moderator disconnect |
| POST | `/api/chats/rooms/{id}/voice/server-mute` | Moderator server mute |
| Hub | `/hubs/voice` | Presence / mute-deafen-speaking |

LiveKit room name format: `voice-crew-{crewId}-room-{roomId}`.

## Client flow (reference)

1. Open `/app/crew/chats/:id/voice`.
2. `join` → LiveKit connect → SignalR `JoinVoice`.
3. Chat list shows occupants via `VoicePresenceService` without joining.
4. Joining another voice channel auto-leaves the previous one (same crew).

## Security reminders

- Rotate `ApiKey` / `ApiSecret` if leaked.
- Never put the API secret in the Angular bundle or Capacitor config.
- Native apps need microphone permission strings — [NATIVE-APPS.md](./NATIVE-APPS.md).
