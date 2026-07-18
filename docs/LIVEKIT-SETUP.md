# LiveKit voice setup (step-by-step)

Crew voice uses **LiveKit** as the SFU and ASP.NET + SignalR for presence. The Angular client never receives the API secret — only short-lived tokens from your API.

Related: [VOICE-QA.md](./VOICE-QA.md), [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md).

---

## Path A — Local development (Docker)

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

### A.2 Point the API at local LiveKit

`LiberationFleet.Server/appsettings.json` or user secrets / `.env`:

```json
"LiveKit": {
  "Host": "ws://localhost:7880",
  "ApiKey": "devkey",
  "ApiSecret": "secretsecretsecretsecretsecretsecret12",
  "TokenTtlMinutes": 360
}
```

Env equivalents:

- `LiveKit__Host`
- `LiveKit__ApiKey`
- `LiveKit__ApiSecret`
- `LiveKit__TokenTtlMinutes`

Match `ApiKey` / `ApiSecret` to `infrastructure/livekit.yaml`.

### A.3 Smoke test

1. Run API + client; sign in; open a crew chat room.
2. Open `/app/crew/chats/:id/voice` (pass 18+ gate if shown).
3. Join — browser should request microphone; LiveKit connects; presence appears on the chat list.
4. Run through [VOICE-QA.md](./VOICE-QA.md).

---

## Path B — Production (LiveKit Cloud) — recommended

### B.1 Create a LiveKit Cloud project

1. Go to [https://cloud.livekit.io](https://cloud.livekit.io) and sign up / sign in.
2. **Create project** (pick a region close to your Azure region).
3. Open **Settings → Keys** (or project overview).
4. Copy:
   - **WebSocket URL** — looks like `wss://xxxxx.livekit.cloud`
   - **API Key**
   - **API Secret**

### B.2 Configure Azure App Service + Key Vault

1. Edit `infrastructure/terraform/environments/production.tfvars` (and staging if you want voice there):

   ```hcl
   livekit_host = "wss://xxxxx.livekit.cloud"
   ```

2. Apply Terraform for that environment (see [AZURE-GO-LIVE.md](./AZURE-GO-LIVE.md)):

   ```bash
   cd infrastructure/terraform
   terraform apply -var-file=environments/production.tfvars
   ```

3. Azure Portal → production **Key Vault** → Secrets:
   - `LiveKit-ApiKey` → New version → paste API Key
   - `LiveKit-ApiSecret` → New version → paste API Secret
4. Restart the Web App.
5. Confirm App Service settings include `LiveKit__Host` = your `wss://` URL (from Terraform).

### B.3 Verify in production

1. Open the production site → crew → voice channel.
2. Join from two browsers / devices.
3. Confirm audio, mute, leave, and sidebar presence.
4. If join fails: check Key Vault values, `wss://` (not `ws://`) host, and browser console / API logs for token mint errors.

---

## Path C — Self-hosted LiveKit + TURN (advanced)

Use this only if you cannot use LiveKit Cloud.

1. Provision a VM or Container Apps environment that allows **UDP** for WebRTC/TURN.
2. Deploy LiveKit Server + coturn (or equivalent) with public DNS and TLS (`wss://`).
3. Set strong API key/secret and TURN credentials; never ship secrets to the Angular client.
4. Wire `LiveKit__Host`, `LiveKit__ApiKey`, `LiveKit__ApiSecret` the same way as Path B.
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
