# LiveKit voice setup (dev)

Discord-style crew voice uses self-hosted LiveKit as the SFU and ASP.NET + SignalR for presence.

## Start infrastructure

```bash
docker compose -f docker-compose.dev.yml up -d db livekit coturn
```

- LiveKit WebSocket: `ws://localhost:7880`
- Coturn TURN: UDP/TCP `3478` (host network), credentials `livekit` / `livekitturn`
- Config: `infrastructure/livekit.yaml` (API keys + TURN servers)

## API configuration

`LiberationFleet.Server/appsettings.json` (or env overrides):

```json
"LiveKit": {
  "Host": "ws://localhost:7880",
  "ApiKey": "devkey",
  "ApiSecret": "secretsecretsecretsecretsecretsecret12",
  "TokenTtlMinutes": 360
}
```

Env equivalents (see `.env.example`):

- `LiveKit__Host`
- `LiveKit__ApiKey`
- `LiveKit__ApiSecret`
- `LiveKit__TokenTtlMinutes`

**Production:** rotate `ApiKey`/`ApiSecret`, set `Host` to your public `wss://` URL, configure TURN with a real realm/DNS, and never ship the API secret to the Angular client.

## Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/chats/rooms/{id}/voice/join` | Mint LiveKit token + create session |
| POST | `/api/chats/rooms/{id}/voice/leave` | Leave + cleanup |
| GET | `/api/chats/voice/presence?crewId=` | Sidebar snapshot |
| POST | `/api/chats/rooms/{id}/voice/disconnect` | Moderator disconnect |
| POST | `/api/chats/rooms/{id}/voice/server-mute` | Moderator server mute |
| Hub | `/hubs/voice` | Presence / mute-deafen-speaking |

LiveKit room name format: `voice-crew-{crewId}-room-{roomId}`.

## Client flow

1. Open `/app/crew/chats/:id/voice` (18+ gate if needed).
2. `join` → LiveKit connect → SignalR `JoinVoice`.
3. Chat list shows occupants via `VoicePresenceService` without joining.
4. Joining another voice channel auto-leaves the previous one (same crew).

See `docs/VOICE-QA.md` for the manual test matrix.
