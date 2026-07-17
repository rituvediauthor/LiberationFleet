# Azure go-live (web API + SPA)

Hosts the combined ASP.NET + Angular container used by **web** and by **native apps** as the API backend.

Full Terraform details: [`infrastructure/terraform/README.md`](../infrastructure/terraform/README.md).  
Pipeline: [`azure-pipelines.yml`](../azure-pipelines.yml), [`.azure/pipelines/README.md`](../.azure/pipelines/README.md).

---

## Step-by-step

### 1. Accounts

1. Create / select an **Azure subscription**.
2. Create an **Azure DevOps** project (or adapt pipeline to GitHub Actions later).
3. Create service connection `azure-liberationfleet` (Workload identity federation preferred).
4. Create ADO Environments `staging` and `production` (approval check on production).

### 2. Bootstrap Terraform state

```bash
cd infrastructure/terraform/bootstrap
terraform init
terraform apply -var="location=eastus"
```

Copy outputs into `environments/staging.backend.hcl` and `production.backend.hcl` (gitignored). Fill variable groups per `.azure/pipelines/README.md`.

### 3. First infrastructure apply (staging)

```bash
cd infrastructure/terraform
terraform init -backend-config=environments/staging.backend.hcl
terraform apply -var-file=environments/staging.tfvars
```

Note outputs: `web_app_name`, `acr_login_server`, `resource_group_name`, `app_public_url`, `key_vault_name`.

### 4. Secrets

In Key Vault (placeholders with `ignore_changes` must be set manually):

| Secret | Purpose |
|--------|---------|
| `Stripe-SecretKey` / `Stripe-WebhookSecret` | Donations |
| `LiveKit-ApiKey` / `LiveKit-ApiSecret` | Voice |
| `ReportEvidence-VendorApiKey` | Report vendor |
| (+ auto) JWT, SQL, report AES, deep-freeze connection | Created by Terraform |

Also set App Settings / Key Vault refs for CORS including:

- Your custom domain
- `capacitor://localhost`, `https://localhost`, `ionic://localhost` (native)

### 5. Deploy the container

Option A — pipeline on `main` (preferred).  
Option B — manual:

```bash
az acr login --name <acr>
docker build -t <login_server>/liberationfleet:<tag> -f LiberationFleet.Server/Dockerfile .
docker push <login_server>/liberationfleet:<tag>
az webapp config container set --name <web_app> --resource-group <rg> \
  --docker-custom-image-name <login_server>/liberationfleet:<tag> \
  --docker-registry-server-url https://<login_server>
az webapp restart --name <web_app> --resource-group <rg>
```

### 6. Verify

- [ ] `https://<app>/` loads SPA
- [ ] Register / login works
- [ ] SignalR (notifications) connects (WebSockets on App Service)
- [ ] Stripe webhook test event succeeds
- [ ] LiveKit token mint succeeds when keys set
- [ ] EF migrations applied (startup migrate)

### 7. Custom domain + TLS

1. Add custom domain on the Web App.
2. Bind managed certificate.
3. Update `Stripe__PublicAppBaseUrl`, CORS, and native `apiBaseUrl` to the custom domain.
4. Point DNS CNAME/A as Azure instructs.

### 8. Production

Repeat Terraform with `production.tfvars`, fill production variable group, approve ADO Environment, deploy. Keep **single App Service instance** until Azure SignalR / Redis backplane is added.

### 9. Point mobile apps at Azure

Set `environment.native.ts` `apiBaseUrl` to the production HTTPS origin, then `npm run cap:sync` and ship store builds ([STORE-SUBMISSION.md](./STORE-SUBMISSION.md)).

---

## Scale / ops reminders

| Topic | Action |
|-------|--------|
| SignalR multi-instance | Add Azure SignalR or Redis before scaling out |
| SQL | Serverless pause OK for staging; watch cold starts |
| Backups | Enable Azure SQL PITR / long-term retention for prod |
| Deep freeze | Confirm `MediaDeepFreeze__Provider=azure` in prod |
| Cost | ACR Basic, App Service B1/P0v3, SQL serverless — review monthly |
