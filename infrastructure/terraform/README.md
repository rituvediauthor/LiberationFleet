# LiberationFleet — Azure infrastructure (Terraform)

Hosts the combined ASP.NET + Angular container (see `LiberationFleet.Server/Dockerfile`) on **Azure App Service (Linux)**, with **Azure SQL**, **Key Vault**, **ACR**, and **Application Insights**.

Voice (LiveKit + TURN) is **not** provisioned here — use [LiveKit Cloud](https://livekit.io/cloud) or a separate VM/Container Apps stack with UDP, then set `livekit_host` and Key Vault secrets `LiveKit-ApiKey` / `LiveKit-ApiSecret`.

Media deep freeze (chat/forum photos & videos older than 60 days) uses an Azure Storage account (`modules/deep-freeze-storage`) with Cool blob tier; see [`docs/MEDIA-DEEP-FREEZE.md`](../../docs/MEDIA-DEEP-FREEZE.md).

## Architecture

```
Browser ──HTTPS/WSS──► App Service (SPA + API + SignalR)
                            │
              ┌─────────────┼──────────────┬──────────────────┐
              ▼             ▼              ▼                  ▼
           Azure SQL    Key Vault   App Insights    Blob (deep freeze)
```
**Scale note:** SignalR is in-process. Keep **one App Service instance** until you add Azure SignalR or a Redis backplane.

## Prerequisites

- Azure subscription + Owner/Contributor
- Terraform >= 1.6
- Azure CLI logged in (`az login`)
- Azure DevOps (or adapt the pipeline to GitHub Actions)

## One-time bootstrap (remote state)

```bash
cd infrastructure/terraform/bootstrap
terraform init
terraform apply -var="location=eastus"
```

Copy the `backend_hcl_snippet` output into:

- `environments/staging.backend.hcl`
- `environments/production.backend.hcl` (change `key` to `production.terraform.tfstate`)

These `*.backend.hcl` files are gitignored.

## Apply (local)

```bash
cd infrastructure/terraform

terraform init -backend-config=environments/staging.backend.hcl
terraform plan  -var-file=environments/staging.tfvars
terraform apply -var-file=environments/staging.tfvars
```

After apply:

1. Push an image to ACR (pipeline does this), or manually:
   ```bash
   az acr login --name <acr_name>
   docker build -t <login_server>/liberationfleet:<tag> -f LiberationFleet.Server/Dockerfile .
   docker push <login_server>/liberationfleet:<tag>
   az webapp config container set \
     --name <web_app_name> \
     --resource-group <rg> \
     --docker-custom-image-name <login_server>/liberationfleet:<tag>
   ```
2. Update Key Vault secrets for Stripe / LiveKit / report vendor (placeholders ignore_changes).
3. Point Stripe webhook to `https://<app>/api/donations/stripe/webhook`.

## Secrets

| Key Vault secret | App setting |
|------------------|-------------|
| `ConnectionStrings-DefaultConnection` | `ConnectionStrings__DefaultConnection` |
| `Jwt-SecretKey` | `Jwt__SecretKey` |
| `ReportEvidence-AesKeyBase64` | `ReportEvidence__AesKeyBase64` |
| `Stripe-SecretKey` | `Stripe__SecretKey` |
| `Stripe-WebhookSecret` | `Stripe__WebhookSecret` |
| `LiveKit-ApiKey` | `LiveKit__ApiKey` |
| `LiveKit-ApiSecret` | `LiveKit__ApiSecret` |
| `ReportEvidence-VendorApiKey` | `ReportEvidence__VendorApiKey` |

JWT and report AES keys are generated on first apply. SQL password is random and stored only in Key Vault.

## CI/CD

See root `azure-pipelines.yml`:

1. **Build** — restore, test, Docker build
2. **Publish** — push image to ACR
3. **Infrastructure** — `terraform plan` / `apply` (environment approvals for production)
4. **Deploy** — update App Service container tag + restart

Required Azure DevOps variable groups / service connections are documented in that file.
