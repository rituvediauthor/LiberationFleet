# Voice chat QA matrix

Manual checks for Discord-style crew voice (LiveKit + SignalR presence).

## Prerequisites

- `docker compose -f docker-compose.dev.yml up -d livekit coturn`
- API + client running with matching LiveKit keys (`docs/LIVEKIT-SETUP.md`)
- Two browsers (or profiles), same crew, encryption unlocked

## Core audio

| # | Scenario | Expected |
|---|----------|----------|
| 1 | User A and B join the same voice channel | Both hear each other; both appear in participant list |
| 2 | A mutes | B no longer hears A; mute badge on A |
| 3 | A deafens | A mic muted + remote audio silenced; deafen badge |
| 4 | A disconnects | A leaves LiveKit + presence; B sidebar/list updates |

## One channel per crew

| # | Scenario | Expected |
|---|----------|----------|
| 5 | A in channel 1 opens channel 2 | A auto-leaves 1; presence moves to 2; sidebar updates |
| 6 | Chat list (not in voice page) | Occupants visible under voice cards without joining |

## Access / safety

| # | Scenario | Expected |
|---|----------|----------|
| 7 | 18+ Ask preference + adult voice room | Gate before connect; confirm then joins |
| 8 | 18+ Block preference | Join API rejects; room treated as unavailable |
| 9 | Non-member / text room join | Rejected |

## Reliability / moderation

| # | Scenario | Expected |
|---|----------|----------|
| 10 | Mic permission denied | Error toast; no silent hang |
| 11 | Brief network blip | Reconnecting indicator; presence re-subscribes after SignalR reconnect |
| 12 | Moderator server mute | Target forced muted; badge shown; unmute disabled until cleared |
| 13 | Moderator disconnect | Target removed from LiveKit + presence |
| 14 | NAT / non-localhost | With TURN up, audio still works off LAN (coturn credentials match livekit.yaml) |

## Device settings

| # | Scenario | Expected |
|---|----------|----------|
| 15 | Preferences → Voice | Input/output device selection persists |
| 16 | In-channel Devices panel | Preference applied to active session |
