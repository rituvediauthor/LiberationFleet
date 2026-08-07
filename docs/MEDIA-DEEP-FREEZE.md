# Media deep freeze

Chat and forum **message text** stays in SQL. **Photos, videos, and audio** attached to those messages (`ImageAsset` / `VideoAsset` / `AudioAsset`) that are older than **60 days** are moved from SQL `EncryptedContentEnvelopes.Ciphertext` into cold storage (local disk in dev, Azure Blob Cool in production).

**Video/audio** are also offloaded **immediately on upload** (not only after AgeDays): JSON path stores base64 text blobs (`.cipher`); binary path (`PUT /api/crypto/content/bytes`) stores raw AES-GCM bytes (`.cipher.bin`) and supports up to **~300 MB** plaintext video. Clients download via `GET /api/crypto/content/bytes`.

## Behavior

| Item | Behavior |
|------|----------|
| What freezes | Media asset envelopes only |
| What stays hot | Chat/forum/DM message envelopes (small text + attachment metadata) |
| Age | `MediaDeepFreeze:AgeDays` (default 60 ≈ two months); video/audio also freeze on upsert |
| Job | `MediaDeepFreezeHostedService` every 6 hours |
| Read path | `GET /api/crypto/content` hydrates legacy base64 cold blobs; binary cold blobs use `GET /api/crypto/content/bytes` |
| Upload (large video/audio) | `PUT /api/crypto/content/bytes` with raw body + `X-LF-Nonce` (requires blob store enabled) |
| E2EE | Server never decrypts; cold blobs are opaque ciphertext |
| Kestrel body limit | ~320 MB (`Program.cs`) — raise any reverse-proxy / App Service ARR timeouts separately for slow cellular uploads |

## Configuration

```json
"MediaDeepFreeze": {
  "Enabled": true,
  "AgeDays": 60,
  "BatchSize": 50,
  "MinimumCiphertextChars": 4096,
  "Provider": "local",
  "LocalRootPath": "App_Data/deep-freeze",
  "AzureConnectionString": "",
  "AzureContainerName": "media-deep-freeze"
}
```

Production (Terraform): `Provider=azure` and connection string from Key Vault. Large video uploads **fail** if the blob store is disabled.

## Schema

New columns on `EncryptedContentEnvelopes`: `StorageTier`, `ColdBlobPath`, `FrozenAt`, `CiphertextCharLength`.
