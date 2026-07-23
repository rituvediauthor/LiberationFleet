# Azure go-live (web API + SPA)

Hosts the combined ASP.NET + Angular container used by **web** and by **native apps** as the API backend.

| Doc | Role |
|-----|------|
| This file | End-to-end Azure account → first staging URL → production |
| [`infrastructure/terraform/README.md`](../infrastructure/terraform/README.md) | Terraform modules & outputs reference |
| [`.azure/pipelines/README.md`](../.azure/pipelines/README.md) | Variable groups & pipeline wiring |
| [`LAUNCH-CHECKLIST.md`](./LAUNCH-CHECKLIST.md) | Master list (legal, stores, third parties) |

**What you will have when finished (staging):** an HTTPS App Service URL serving the SPA + API, Azure SQL, Key Vault, ACR, Application Insights, and (optionally) a CI/CD pipeline on `main`.

---

## Before you start

Install and sign in:

1. **Azure CLI** — [Install](https://learn.microsoft.com/cli/azure/install-azure-cli), then:
   ```bash
   az login
   az account set --subscription "<YOUR_SUBSCRIPTION_NAME_OR_ID>"
   ```
2. **Terraform** ≥ 1.6 — [Install](https://developer.hashicorp.com/terraform/install).
3. **Docker Desktop** (for manual image push) — optional if you only use the ADO pipeline.
4. **Git** + a local clone of this repo.
5. Permissions: Azure subscription **Owner** or **Contributor** + ability to create app registrations (for the DevOps service connection). In Azure DevOps, **Project Administrator** (or equivalent) for service connections / environments / pipelines.

Pick a region (examples use `eastus`). Keep it consistent for bootstrap + staging + production.

---

## Step 1 — Azure subscription

1. Sign in at [portal.azure.com](https://portal.azure.com).
2. Create or select a subscription (Pay-As-You-Go, Visual Studio benefit, etc.).
3. Note:
   - **Subscription name**
   - **Subscription ID** (Subscriptions blade → copy)
4. Optional but recommended: create a billing alert (Cost Management → Budgets).

---

## Step 2 — Azure DevOps project

1. Go to [dev.azure.com](https://dev.azure.com) → create an **organization** if you do not have one.
2. **New project** → name it (e.g. `LiberationFleet`) → Private → Create.
3. Connect the repo (if code is still only on GitHub):
   - **Repos** → **Import repository**, **or**
   - Add Azure Repos as a remote and push `main`, **or**
   - In Pipelines, use a GitHub service connection later (pipeline YAML still works; service connection steps below stay the same).

---

## Step 3 — Service connection `azure-liberationfleet`

Lets pipelines run Terraform and deploy to App Service / ACR. Prefer **Workload identity federation** (no long-lived client secret).

### 3.1 Create the connection

1. Azure DevOps project → **Project settings** (bottom left).
2. **Pipelines** → **Service connections**.
3. **New service connection**.
4. **Azure Resource Manager** → **Next**.
5. Choose **Workload Identity federation (automatic)** if offered.  
   If not: **Workload Identity federation (manual)** and follow the Azure portal prompts to create/link the Entra ID app.
6. Fill in:
   - **Scope level**: Subscription  
   - **Subscription**: your Azure subscription  
   - **Resource group**: leave empty (subscription-wide) unless you intentionally lock scope  
   - **Service connection name**: exactly `azure-liberationfleet`  
   - **Grant access permission to all pipelines**: enable (or authorize the pipeline on first run)
7. **Save**.

### 3.2 Verify Azure IAM

1. Azure Portal → **Subscriptions** → your subscription → **Access control (IAM)** → **Role assignments**.
2. Find the identity created for the service connection (often an App registration / managed identity name matching the connection).
3. It should have at least **Contributor**.  
   If later Terraform fails creating role assignments, also grant **User Access Administrator** (or a custom role that can assign roles).

### 3.3 Sanity check

Service connections list shows **`azure-liberationfleet`**, Azure Resource Manager, Workload Identity federation.

---

## Step 4 — ADO Environments `staging` and `production`

The pipeline uses `environment: staging` and `environment: production`.

### 4.1 Create environments

1. ADO → **Pipelines** → **Environments**.
2. **Create environment** → Name: `staging` → Resource: **None** → **Create**.
3. Repeat with Name: `production`.

### 4.2 Approvals on production only

1. Open **production** → **⋮** / **…** → **Approvals and checks**.
2. **+** → **Approvals**.
3. Add yourself (and any co-owners) as Approvers.
4. Optional: allow approvers to approve their own runs (useful if you are solo).
5. **Create**.
6. Leave **staging** with no approval checks.

Optional later: **Branch control** on production → allow only `refs/heads/main`.

---

## Step 5 — Bootstrap Terraform remote state (one time)

Creates a resource group + storage account + container that holds Terraform state for staging and production.

### 5.1 Apply bootstrap

From a machine with Azure CLI logged in:

```bash
cd infrastructure/terraform/bootstrap
terraform init
terraform apply -var="location=eastus"
```

Type `yes` when prompted. Wait for completion.

### 5.2 Capture outputs

```bash
terraform output
```

You need at least:

| Output | Example |
|--------|---------|
| `resource_group_name` | `rg-lfleet-tfstate` |
| `storage_account_name` | `stlfeetxxxxxx` |
| `container_name` | `tfstate` |
| `backend_hcl_snippet` | ready-to-paste HCL |

### 5.3 Create backend config files (gitignored)

```bash
cd ../environments
cp staging.backend.hcl.example staging.backend.hcl
cp production.backend.hcl.example production.backend.hcl
```

Edit **`staging.backend.hcl`** using bootstrap values (or paste `backend_hcl_snippet`):

```hcl
resource_group_name  = "rg-lfleet-tfstate"
storage_account_name = "stlfeetxxxxxx"   # your real name
container_name       = "tfstate"
key                  = "staging.terraform.tfstate"
```

Edit **`production.backend.hcl`** the same way, but:

```hcl
key = "production.terraform.tfstate"
```

Do **not** commit these files (they are gitignored).

### 5.4 Fill ADO variable group `liberationfleet-shared`

1. ADO → **Pipelines** → **Library** → **+ Variable group**.
2. Name: exactly `liberationfleet-shared`.
3. Add variables (non-secret):

| Name | Value |
|------|--------|
| `TF_STATE_RG` | bootstrap `resource_group_name` |
| `TF_STATE_STORAGE` | bootstrap `storage_account_name` |
| `TF_STATE_CONTAINER` | `tfstate` |

4. **Save**.

---

## Step 6 — First infrastructure apply (staging, local)

Creates App Service, ACR, SQL, Key Vault, App Insights, deep-freeze storage, etc.

### 6.1 Review / edit `staging.tfvars`

Open `infrastructure/terraform/environments/staging.tfvars`. Prefer `location = "westus2"` on new subscriptions — `eastus` / `eastus2` often block SQL create. Optionally set `sql_firewall_rules` with your public IP for SSMS later.

### 6.2 Init + apply

```bash
cd infrastructure/terraform
# Quote values in PowerShell so flags are not misparsed.
terraform init -backend-config="environments/staging.backend.hcl"
terraform plan  -var-file="environments/staging.tfvars"
terraform apply -var-file="environments/staging.tfvars"
```

Approve with `yes`. First apply can take 10–20+ minutes (SQL especially).

#### If apply fails: SQL “ProvisioningDisabled” in this region

New subscriptions are often blocked from creating SQL servers in popular regions (especially `eastus` / `eastus2`).

1. Edit `staging.tfvars` → set `location` to another region (try `westus2`, `centralus`, or `northcentralus`).
2. If a Key Vault name collides (`VaultAlreadyExists`), purge the soft-deleted vault first:
   ```powershell
   az keyvault list-deleted -o table
   az keyvault purge --name lfleetstagingkv
   ```
3. Clean the partial stack, then re-apply:

```powershell
terraform destroy -var-file="environments/staging.tfvars"
terraform apply -var-file="environments/staging.tfvars"
```

Or delete resource group `rg-lfleet-staging` in the portal, then `terraform apply` again.

#### If apply fails: App Service “No available instances” (409)

Transient regional capacity. Wait 15–30 minutes and re-run `terraform apply`, or switch `location` and rebuild as above.

#### If apply fails: App Service “without additional quota” / Total VMs: 0

Your subscription has **0** App Service plan quota in that region. Request an increase (usually to at least **1**):

1. Portal → **Subscriptions** → your subscription → **Usage + quotas** (or search **Quotas**).
2. Filter provider **App Service** / region matching `location` in tfvars.
3. Request increase for App Service plans / compute (ask for at least **1**; **10** is fine).
4. Or: **Help + support** → **Create a support request** → Issue type **Service and subscription limits (quotas)** → App Service → submit.
5. After approval (often minutes–hours), re-run `terraform apply`.

Providers `Microsoft.Web` and `Microsoft.Sql` must show **Registered** (`az provider show -n Microsoft.Web` / `Microsoft.Sql`).

### 6.3 Record outputs

```bash
terraform output
```

Write these down:

| Output | Used for |
|--------|----------|
| `resource_group_name` | Deploy / variable group |
| `web_app_name` | Deploy / variable group |
| `acr_name` | Docker push / variable group |
| `acr_login_server` | Docker / variable group |
| `app_public_url` | Browser tests, Stripe base URL |
| `key_vault_name` | Secrets |
| `web_app_default_hostname` | `*.azurewebsites.net` host |

### 6.4 Fill ADO variable group `liberationfleet-staging`

1. Library → **+ Variable group** → Name: `liberationfleet-staging`.
2. Variables:

| Name | Value (from terraform output) |
|------|-------------------------------|
| `ENVIRONMENT` | `staging` |
| `AZURE_RESOURCE_GROUP` | `resource_group_name` |
| `WEB_APP_NAME` | `web_app_name` |
| `ACR_NAME` | `acr_name` |
| `ACR_LOGIN_SERVER` | `acr_login_server` |

3. **Save**. Link this group to your pipeline when prompted (or Pipeline → Edit → … → Variable groups).

---

## Step 7 — Secrets in Key Vault

Terraform creates placeholder secrets with `ignore_changes` for Stripe / LiveKit / report vendor. You must set real values (or leave placeholders until you wire those features).

### 7.1 Open Key Vault

1. Azure Portal → search for `key_vault_name` from outputs.
2. If access is denied: **Access control (IAM)** → grant your user **Key Vault Secrets Officer** (RBAC), or configure Access policies if the vault still uses that model.
3. **Secrets**.

### 7.2 Set or update secrets

For each secret below: open it → **New version** → paste value → Create.

| Secret name | When required | Where to get it |
|-------------|---------------|-----------------|
| `Stripe-SecretKey` | Before real donations | Stripe Dashboard → API keys (`sk_test_…` then `sk_live_…`) — [DONATION-SETUP.md](./DONATION-SETUP.md) |
| `Stripe-WebhookSecret` | Before donation totals work | Stripe webhook signing secret (`whsec_…`) |
| `LiveKit-ApiKey` | Before prod voice | LiveKit Cloud project — [LIVEKIT-SETUP.md](./LIVEKIT-SETUP.md) |
| `LiveKit-ApiSecret` | Before prod voice | LiveKit Cloud |
| `ReportEvidence-VendorApiKey` | Before vendor ops API | Generate a long random string; share with contractor — [REPORT-VENDOR-WEBHOOK.md](./REPORT-VENDOR-WEBHOOK.md) |

**Created automatically by Terraform (do not overwrite casually):**

- `ConnectionStrings-DefaultConnection`
- `Jwt-SecretKey`
- `ReportEvidence-AesKeyBase64`
- Deep-freeze storage connection (as configured in modules)

### 7.3 App settings that are not Key Vault secrets

Terraform already sets CORS for Capacitor origins and the default app URL. After you add a **custom domain**, add another CORS origin (App Service → Configuration → Application settings), e.g.:

- `Cors__AllowedOrigins__5` = `https://your.custom.domain`

Also set when you know the public URL:

- `Stripe__PublicAppBaseUrl` = `https://your-host` (no trailing slash)

Restart the Web App after changing app settings.

### 7.4 LiveKit host (Terraform variable)

If using LiveKit Cloud, set `livekit_host` in `staging.tfvars` / `production.tfvars`:

```hcl
livekit_host = "wss://your-project.livekit.cloud"
```

Then `terraform apply` again for that environment so the App Service setting updates.

---

## Step 8 — Deploy the container image

### Option A — Azure Pipeline (preferred)

1. ADO → **Pipelines** → **New pipeline**.
2. Select your repo → **Existing Azure Pipelines YAML file** → path `/azure-pipelines.yml`.
3. **Save** (do not run yet if variable groups are incomplete).
4. Ensure the pipeline can use:
   - Service connection `azure-liberationfleet`
   - Variable groups `liberationfleet-shared` and `liberationfleet-staging`
5. Push or run on **`main`**. Staging stage runs after Build succeeds.
6. Watch **Deploy staging**. Authorize any permission prompts the first time.
7. When finished, open `app_public_url` from Terraform outputs.

**Note:** Production stage also runs on `main` after staging and will wait for **environment approval** on `production`. Until production Terraform + variable group exist, either:

- Create production infra + `liberationfleet-production` first (Step 11), **or**
- Temporarily comment out / skip the production stage (not ideal), **or**
- Reject the production approval until ready.

### Option B — Manual Docker deploy

Replace placeholders from Terraform outputs:

```bash
# From repo root
az acr login --name <acr_name>

docker build -t <acr_login_server>/liberationfleet:manual -f LiberationFleet.Server/Dockerfile .

docker push <acr_login_server>/liberationfleet:manual

az webapp config container set \
  --name <web_app_name> \
  --resource-group <resource_group_name> \
  --docker-custom-image-name <acr_login_server>/liberationfleet:manual \
  --docker-registry-server-url https://<acr_login_server>

az webapp restart \
  --name <web_app_name> \
  --resource-group <resource_group_name>
```

Wait 1–3 minutes, then open `https://<web_app_default_hostname>/`.

---

## Step 9 — Verify staging

Work through this list against `app_public_url`:

- [ ] Home / SPA loads (not a Docker or 503 error page)
- [ ] Register a test user and log in
- [ ] Notifications hub connects (browser DevTools → Network → WS; App Service has WebSockets enabled in Terraform)
- [ ] Create or open a crew; open chat; send a message
- [ ] EF migrations: app starts without DB errors (startup migrate). If login fails with SQL errors, check Key Vault connection string and SQL firewall
- [ ] (When Stripe test keys set) Donation Checkout opens — [DONATION-SETUP.md](./DONATION-SETUP.md)
- [ ] (When LiveKit set) Voice join mints a token — [LIVEKIT-SETUP.md](./LIVEKIT-SETUP.md)

If the container fails to start: App Service → **Log stream** / **Deployment Center** / **Container settings** for pull/auth errors.

---

## Step 10 — Custom domain + TLS

Do this when you own a domain and want a branded URL (recommended before production Stripe + store listings).

1. Azure Portal → Web App → **Custom domains** → **Add custom domain**.
2. Follow DNS instructions at your registrar (usually a CNAME to `*.azurewebsites.net`, or A + TXT as shown).
3. After domain validates → **Add binding** → **App Service Managed Certificate** (free) → TLS.
4. Update:
   - `Stripe__PublicAppBaseUrl` = `https://your.domain`
   - CORS: add `https://your.domain` as an allowed origin
   - Stripe webhook URL to `https://your.domain/api/donations/stripe/webhook`
5. Restart the Web App.
6. Confirm `https://your.domain` loads with a valid certificate.

---

## Step 11 — Production

### 11.1 Apply production Terraform

```bash
cd infrastructure/terraform
terraform init -reconfigure -backend-config="environments/production.backend.hcl"
terraform plan  -var-file="environments/production.tfvars"
terraform apply -var-file="environments/production.tfvars"
```

Review `production.tfvars` for SKU / region. Prefer a stronger SQL SKU and backups for prod.

### 11.2 Variable group `liberationfleet-production`

Same keys as staging, values from **production** `terraform output`.

### 11.3 Secrets

Repeat Step 7 for the **production** Key Vault (use **live** Stripe keys, production LiveKit project, etc.).

### 11.4 Deploy

1. Ensure production variable group is linked to the pipeline.
2. Run / push `main`.
3. When **Deploy production** waits on approval → open the run → **Approve**.
4. Verify production URL (Step 9 checklist).

**Scale rule:** keep **one** App Service instance until Azure SignalR or a Redis backplane is added (in-process SignalR).

### 11.5 SQL backups

Azure Portal → SQL database → **Backup** / configure PITR and long-term retention for production.

---

## Step 12 — Point mobile apps at Azure

1. Edit `liberationfleet.client/src/environments/environment.native.ts`:
   ```ts
   apiBaseUrl: 'https://your-production-host'  // no trailing slash
   ```
2. Follow [NATIVE-APPS.md](./NATIVE-APPS.md) (`npm run cap:sync`, device smoke test).
3. Submit stores via [STORE-SUBMISSION.md](./STORE-SUBMISSION.md).

---

## Scale / ops reminders

| Topic | Action |
|-------|--------|
| SignalR multi-instance | Add Azure SignalR or Redis before scaling out |
| SQL | Serverless pause OK for staging; watch cold starts |
| Backups | Enable Azure SQL PITR / LTR for prod |
| Deep freeze | Confirm `MediaDeepFreeze__Provider=azure` in prod — [MEDIA-DEEP-FREEZE.md](./MEDIA-DEEP-FREEZE.md) |
| Cost | ACR Basic, App Service B1/P0v3, SQL serverless — review monthly |

---

## Troubleshooting quick reference

| Symptom | Likely fix |
|---------|------------|
| Pipeline cannot find service connection | Name must be exactly `azure-liberationfleet`; authorize pipeline |
| Terraform backend errors | Wrong `*.backend.hcl`; not logged in (`az login`); storage firewall |
| Container pull fails | ACR permissions for App Service managed identity; image tag missing |
| 502/503 after deploy | Check Log stream; confirm migrations / connection string |
| CORS errors from Capacitor | Keep Capacitor origins; add custom domain origin |
| Stripe totals stay $0 | Webhook secret + endpoint URL wrong |
| Voice join fails | `LiveKit__Host` must be `wss://…`; Key Vault API key/secret set |
