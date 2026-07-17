# Media deep freeze

Chat and forum **message text** stays in SQL. **Photos, videos, and audio** attached to those messages (`ImageAsset` / `VideoAsset` / `AudioAsset`) that are older than **60 days** are moved from SQL `EncryptedContentEnvelopes.Ciphertext` into cold storage (local disk in dev, Azure Blob Cool in production).

## Behavior

| Item | Behavior |
|------|----------|
| What freezes | Media asset envelopes only |
| What stays hot | Chat/forum/DM message envelopes (small text + attachment metadata) |
| Age | `MediaDeepFreeze:AgeDays` (default 60 ≈ two months) |
| Job | `MediaDeepFreezeHostedService` every 6 hours |
| Read path | `GET /api/crypto/content` hydrates ciphertext from cold store in memory |
| E2EE | Server never decrypts; cold blobs are opaque ciphertext |

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

Production (Terraform): `Provider=azure` and connection string from Key Vault.

## Schema

New columns on `EncryptedContentEnvelopes`: `StorageTier`, `ColdBlobPath`, `FrozenAt`, `CiphertextCharLength`.
