# LiberationFleet — Azure infrastructure (Terraform)

Hosts the combined ASP.NET + Angular container (`LiberationFleet.Server/Dockerfile`) on **Azure App Service (Linux)**, with **Azure SQL**, **Key Vault**, **ACR**, **Application Insights**, and **deep-freeze Blob storage**.

**Operator walkthrough (click-by-click):** [`docs/AZURE-GO-LIVE.md`](../../docs/AZURE-GO-LIVE.md)

Voice (LiveKit + TURN) is **not** provisioned here — use [LiveKit Cloud](https://livekit.io/cloud) or a separate stack, then set `livekit_host` and Key Vault secrets. See [`docs/LIVEKIT-SETUP.md`](../../docs/LIVEKIT-SETUP.md).

Media deep freeze: [`docs/MEDIA-DEEP-FREEZE.md`](../../docs/MEDIA-DEEP-FREEZE.md).

## Architecture

```
Browser ──HTTPS/WSS──► App Service (SPA + API + SignalR)
                            │
              ┌─────────────┼──────────────┬──────────────────┐
              ▼             ▼              ▼                  ▼
           Azure SQL    Key Vault   App Insights    Blob (deep freeze)
```

**Scale note:** SignalR is in-process. Keep **one App Service instance** until Azure SignalR or a Redis backplane is added.

## Prerequisites

- Azure subscription (Owner/Contributor)
- Terraform ≥ 1.6
- Azure CLI (`az login`)
- Azure DevOps for CI/CD (optional for first local apply)

## Step-by-step: bootstrap remote state

Detailed version: [AZURE-GO-LIVE Step 5](../../docs/AZURE-GO-LIVE.md#step-5--bootstrap-terraform-remote-state-one-time).

```bash
cd infrastructure/terraform/bootstrap
terraform init
terraform apply -var="location=eastus"
terraform output
```

Copy `backend_hcl_snippet` into:

- `environments/staging.backend.hcl` (key = `staging.terraform.tfstate`)
- `environments/production.backend.hcl` (key = `production.terraform.tfstate`)

Start from `*.backend.hcl.example`. These files are **gitignored**.

## Step-by-step: apply an environment

```bash
cd infrastructure/terraform

# Staging
terraform init -backend-config=environments/staging.backend.hcl
terraform plan  -var-file=environments/staging.tfvars
terraform apply -var-file=environments/staging.tfvars

# Production (separate state file)
terraform init -reconfigure -backend-config=environments/production.backend.hcl
terraform plan  -var-file=environments/production.tfvars
terraform apply -var-file=environments/production.tfvars
```

### Important outputs

| Output | Use |
|--------|-----|
| `resource_group_name` | CLI + ADO `AZURE_RESOURCE_GROUP` |
| `web_app_name` | Deploy + ADO `WEB_APP_NAME` |
| `acr_name` / `acr_login_server` | Docker + ADO |
| `app_public_url` | Browser, Stripe base URL |
| `key_vault_name` | Secrets UI |

## After apply

1. **Deploy an image** — pipeline on `main`, or manual:
   ```bash
   az acr login --name <acr_name>
   docker build -t <login_server>/liberationfleet:<tag> -f LiberationFleet.Server/Dockerfile .
   docker push <login_server>/liberationfleet:<tag>
   az webapp config container set \
     --name <web_app_name> \
     --resource-group <rg> \
     --docker-custom-image-name <login_server>/liberationfleet:<tag> \
     --docker-registry-server-url https://<login_server>
   az webapp restart --name <web_app_name> --resource-group <rg>
   ```
2. **Key Vault** — set Stripe / LiveKit / report vendor secrets (placeholders use `ignore_changes`). See [AZURE-GO-LIVE Step 7](../../docs/AZURE-GO-LIVE.md#step-7--secrets-in-key-vault).
3. **Stripe webhook** → `https://<app>/api/donations/stripe/webhook` ([DONATION-SETUP.md](../../docs/DONATION-SETUP.md)).
4. Optional: set `livekit_host` in `*.tfvars` and re-apply.

## Secrets map

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

## CORS (App Service)

Terraform sets:

- App public URL  
- `capacitor://localhost`, `ionic://localhost`, `https://localhost`, `http://localhost`

After a custom domain, add `Cors__AllowedOrigins__N` for `https://your.domain`.

## CI/CD

See root `azure-pipelines.yml` and [`.azure/pipelines/README.md`](../../.azure/pipelines/README.md).

1. Build / test / Docker  
2. Terraform plan/apply (Environment approvals for production)  
3. Push to ACR + update App Service container tag  

## Modules

| Path | Responsibility |
|------|----------------|
| `modules/resource-group` | RG |
| `modules/container-registry` | ACR |
| `modules/app-service` | Linux Web App + settings |
| `modules/sql` | Azure SQL |
| `modules/key-vault` | Vault + secret placeholders |
| `modules/monitoring` | App Insights |
| `modules/deep-freeze-storage` | Cool-tier blob for media deep freeze |
| `bootstrap/` | Remote state storage only |
