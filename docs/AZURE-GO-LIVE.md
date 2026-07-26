# Azure go-live (web API + SPA)

Hosts the combined ASP.NET + Angular container used by **web** and by **native apps** as the API backend.

| Doc | Role |
|-----|------|
| This file | End-to-end Azure account → first staging URL → production |
| [`infrastructure/terraform/README.md`](../infrastructure/terraform/README.md) | Terraform modules & outputs reference |
| [`.azure/pipelines/README.md`](../.azure/pipelines/README.md) | Variable groups & pipeline wiring |
| [`LAUNCH-CHECKLIST.md`](./LAUNCH-CHECKLIST.md) | Master list (legal, stores, third parties) |

**What you will have when finished (staging):** an HTTPS App Service URL serving the SPA + API, Azure SQL, Key Vault, ACR, Application Insights, and (optionally) a CI/CD pipeline on `master`.

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
   - Add Azure Repos as a remote and push `master`, **or**
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
3. Grant at least:
   - **Contributor** on the subscription (or the `rg-lfleet-*` resource groups)
   - **User Access Administrator** if Terraform must create/delete role assignments (App Service → Key Vault Secrets User, etc.)
   - **Storage Blob Data Contributor** on the tfstate storage account
   - **Key Vault Secrets Officer** on each environment Key Vault (`lfleetstagingkv`, later `lfleetproductionkv`) — required for plan/apply to read secrets; do **not** rely on Terraform to grant this to the pipeline identity (it flips between your user and the SP and breaks CI)

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

Optional later: **Branch control** on production → allow only `refs/heads/master`.

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

## Step 7 — Secrets in staging Key Vault

This step is for **staging only** (after Step 6). Production Key Vault is created later in **Step 11** and filled in **Step 11.3**.

Terraform creates placeholder secrets with `ignore_changes` for Stripe / LiveKit / report vendor. You must set real values (or leave placeholders until you wire those features).

| Environment | Typical Key Vault name | Stripe keys |
|-------------|------------------------|-------------|
| Staging (this step) | `lfleetstagingkv` | **Test** `sk_test_…` / test `whsec_…` |
| Production (Step 11.3) | `lfleetproductionkv` | **Live** `sk_live_…` / live `whsec_…` |

Exact name: `terraform output key_vault_name` for that environment.

### 7.1 Open staging Key Vault

1. Azure Portal → search for staging `key_vault_name` from outputs.
2. If access is denied: **Access control (IAM)** → grant your user **Key Vault Secrets Officer** (RBAC), or configure Access policies if the vault still uses that model.
3. **Secrets**.

### 7.2 Set or update staging secrets

For each secret below: open it → **New version** → paste value → Create.

| Secret name | When required | Where to get it |
|-------------|---------------|-----------------|
| `Stripe-SecretKey` | Before staging donation Checkout | Stripe **Test** API key (`sk_test_…`) — [DONATION-SETUP.md](./DONATION-SETUP.md) Part C |
| `Stripe-WebhookSecret` | Before staging donation totals work | Stripe **Test** webhook signing secret |
| `LiveKit-ApiKey` | Before staging voice | LiveKit Cloud — [LIVEKIT-SETUP.md](./LIVEKIT-SETUP.md) Path B |
| `LiveKit-ApiSecret` | Before staging voice | LiveKit Cloud |
| `ReportEvidence-VendorApiKey` | Before vendor ops API | Generate a long random string — [REPORT-VENDOR-WEBHOOK.md](./REPORT-VENDOR-WEBHOOK.md) |

**Created automatically by Terraform (do not overwrite casually):**

- `ConnectionStrings-DefaultConnection`
- `Jwt-SecretKey`
- `ReportEvidence-AesKeyBase64`
- Deep-freeze storage connection (as configured in modules)

### 7.3 App settings that are not Key Vault secrets (staging)

These live on the **Web App**, not in Key Vault. They are plain app settings (environment variables).

#### Where to look

1. Azure Portal → resource group **`rg-lfleet-staging`** → Web App **`app-lfleet-staging`**.
2. Left menu → **Settings** → **Environment variables** (older UI: **Configuration** → **Application settings**).
3. Find the name in the list (or use search).

#### What Terraform already set (usually just verify)

After Step 6, these should already exist. Confirm they match your staging public URL from `terraform output app_public_url` (typically `https://app-lfleet-staging.azurewebsites.net`, **no trailing slash**):

| Setting | Expected for default staging host |
|---------|-----------------------------------|
| `Stripe__PublicAppBaseUrl` | Same as `app_public_url` |
| `Cors__AllowedOrigins__0` | Same as `app_public_url` |
| `Cors__AllowedOrigins__1` … `__4` | Capacitor / localhost origins (leave as-is) |

If `Stripe__PublicAppBaseUrl` is already correct for `*.azurewebsites.net`, **you do not need to change anything in this step** for donations on the default host.

#### Only if you add a custom domain later (Step 10)

1. In the same Environment variables list, **Add**:
   - Name: `Cors__AllowedOrigins__5`
   - Value: `https://your.custom.domain` (exact origin users will open in the browser)
2. Edit `Stripe__PublicAppBaseUrl` to that same `https://your.custom.domain` (no trailing slash).
3. Click **Apply** / **Save**, then **Restart** the Web App (Overview → Restart).

Until you have a custom domain, skip this subsection.

### 7.4 LiveKit host (Terraform variable)

Skip this subsection until you have a LiveKit Cloud project ([LIVEKIT-SETUP.md](./LIVEKIT-SETUP.md) Path B). Local Docker voice does **not** use this.

Terraform copies `livekit_host` into the App Service setting **`LiveKit__Host`**. The API key/secret are **not** in tfvars — those go in Key Vault (table in §7.2).

#### Staging (do this now when ready for staging voice)

1. Open LiveKit Cloud → your project → **Settings → API Keys -> click key**.
2. Copy the WebSocket URL. It must start with `wss://` (not `ws://`), e.g. `wss://your-project.livekit.cloud`.
3. Edit `infrastructure/terraform/environments/staging.tfvars` and set (or **uncomment**) this line — if it stays commented, `LiveKit__Host` stays blank:

   ```hcl
   livekit_host = "wss://your-project.livekit.cloud"
   ```

4. From `infrastructure/terraform`, re-apply **staging** (quote the path in PowerShell):

   ```powershell
   terraform apply -var-file="environments/staging.tfvars"
   ```

   Enter `yes` when prompted.
5. Portal → `app-lfleet-staging` → **Environment variables** → confirm **`LiveKit__Host`** equals that same `wss://` URL (not empty).
6. Still required for voice: Key Vault secrets `LiveKit-ApiKey` and `LiveKit-ApiSecret` (§7.2), then **Restart** the Web App.

#### Production (later — Step 11)

Do **not** put production LiveKit values in `staging.tfvars`. When production infra exists:

1. Prefer a **separate** LiveKit Cloud project from staging.
2. Set `livekit_host` in `environments/production.tfvars`.
3. `terraform apply -var-file="environments/production.tfvars"` (production backend).
4. Set production Key Vault `LiveKit-ApiKey` / `LiveKit-ApiSecret`, then restart the production Web App.

---

## Step 8 — Deploy the container image

Terraform created an empty App Service that expects image `liberationfleet:latest` in ACR. Until you push an image, the site shows **Application Error / 503**.

Pick **Option A** (Azure DevOps pipeline) or **Option B** (manual Docker from your machine). You only need one.

### 8.0 Prerequisites checklist (do this before Option A)

Confirm these already exist from earlier steps. If any are missing, fix them first — the pipeline will fail otherwise.

| Check | Where | How |
|-------|--------|-----|
| Service connection `azure-liberationfleet` | ADO → **Project settings** (bottom left) → **Pipelines** → **Service connections** | Exact name. If missing, redo [Step 3](#step-3--service-connection-azure-liberationfleet). |
| Environments `staging` and `production` | ADO → **Pipelines** → **Environments** | If missing, redo [Step 4](#step-4--ado-environments-staging-and-production). Production should have an **Approvals** check. |
| Variable group `liberationfleet-shared` | ADO → **Pipelines** → **Library** → **Variable groups** -> **liberationfleet-shared** | Has `TF_STATE_RG`, `TF_STATE_STORAGE`, `TF_STATE_CONTAINER` ([Step 5](#step-5--bootstrap-terraform-remote-state-one-time)). |
| Variable group `liberationfleet-staging` | Same Library page | Has `ENVIRONMENT`, `AZURE_RESOURCE_GROUP`, `WEB_APP_NAME`, `ACR_NAME`, `ACR_LOGIN_SERVER` ([Step 6.4](#64-fill-ado-variable-group-liberationfleet-staging)). |
| Variable group `liberationfleet-production` | Same Library page | **Must exist** even before production infra — the YAML references it at validate time. Create a stub now (same variable *names* as staging; placeholder values OK). Fill real values in Step 11. |
| Staging ACR exists | Azure Portal → `rg-lfleet-staging` → Container registry (e.g. `lfleetstagingacr`) | Created by Terraform in Step 6. |

Typical staging values (yours may match):

| Variable | Example |
|----------|---------|
| `AZURE_RESOURCE_GROUP` | `rg-lfleet-staging` |
| `WEB_APP_NAME` | `app-lfleet-staging` |
| `ACR_NAME` | `lfleetstagingacr` |
| `ACR_LOGIN_SERVER` | `lfleetstagingacr.azurecr.io` |

### Option A — Azure Pipeline (preferred)

#### A.1 Create the pipeline (one time)

1. Open your Azure DevOps project in the browser (not Azure Portal).
2. Left nav → **Pipelines** → **Pipelines**.
3. **New pipeline** (or **Create Pipeline**).
4. Where is your code?
   - **Azure Repos Git** if the repo is in this ADO project, **or**
   - **GitHub** → authorize → pick `rituvediauthor/LiberationFleet` (or your fork).
5. **Configure** → choose **Existing Azure Pipelines YAML file**.
6. Branch: `master`. Path: `/azure-pipelines.yml` → **Continue**.
7. Review the YAML → **Save** (dropdown next to Run) → **Save** (do **not** Run yet if Step 8.0 failed any check).

#### A.2 Let the pipeline use the service connection and variable groups

The YAML already references:

- Service connection name: `azure-liberationfleet`
- Variable groups: `liberationfleet-shared`, `liberationfleet-staging`, and later `liberationfleet-production`

**Authorize the service connection (first run or if builds fail with “service connection” errors):**

1. Open the failed (or waiting) run, **or** go to **Project settings** → **Service connections** → `azure-liberationfleet` → **…** → **Security**.
2. Under **Pipeline permissions**, grant access to your pipeline (or enable “Grant access permission to all pipelines” on the connection).
3. If a run shows **Waiting for permission** / **Authorize resource**, click **Permit** / **Authorize**.

**Link variable groups (if Library prompts you, or if the run says the group was not found):**

1. ADO → **Pipelines** → **Library** → open `liberationfleet-staging` (and `liberationfleet-shared`).
2. Tab **Pipeline permissions** (or **…** → Pipeline permissions).
3. **+** → select your LiberationFleet pipeline → allow.
4. Repeat for `liberationfleet-shared`.
5. **Also create / authorize `liberationfleet-production` now** (required for the pipeline to validate, even if you will Reject production deploys until Step 11):

   1. Library → **+ Variable group** → Name: exactly `liberationfleet-production`.
   2. Add the same five variables as staging (`ENVIRONMENT`, `AZURE_RESOURCE_GROUP`, `WEB_APP_NAME`, `ACR_NAME`, `ACR_LOGIN_SERVER`).
   3. For now you can copy staging values or use placeholders (e.g. `ENVIRONMENT` = `production`, others = `pending`). Real production outputs come in Step 11.
   4. **Save** → **Pipeline permissions** → allow your LiberationFleet pipeline (or authorize when the run prompts).

   If you skip this group, the run fails immediately with: *Variable group liberationfleet-production could not be found*.

#### A.3 Run a staging deploy

**Automatic:** merge or push a commit to **`master`** (docs-only changes under `docs/` are excluded by the YAML and will **not** trigger).

**Manual (recommended for first deploy):**

1. **Pipelines** → your pipeline → **Run pipeline**.
2. Branch: `master` → **Run**.
3. Open the run. Watch stages in order: **Build and test** → **Deploy staging**.
4. If **Deploy staging** asks to use environment `staging` or a resource, **Permit**.
5. Wait until **Deploy staging** is green (often 10–20+ minutes the first time: build, Terraform, Docker push, App Service restart).

#### A.4 Production stage on the same run (what to do)

On `master`, after staging succeeds, **Deploy production** also starts and will **wait for approval** on environment `production`.

Until Step 11 is done (production Terraform + `liberationfleet-production` variable group):

1. Open the run → find **Deploy production** waiting.
2. Click **Reject** (or let it sit — do **not** Approve yet).

That is normal. Staging is still deployed. Come back to Approve only after Step 11.

#### A.5 Confirm the image and site

1. Azure Portal → `lfleetstagingacr` (or your ACR) → **Repositories** → `liberationfleet` should list tags (`latest` and a short git SHA).
2. Browser → `https://app-lfleet-staging.azurewebsites.net/` (or `terraform output app_public_url`).
3. You should get the SPA, not “Application Error”. If 503 persists a few minutes, check App Service → **Log stream** / **Deployment Center**.

---

### Option B — Manual Docker deploy (no pipeline)

Use this if ADO is not ready. From a machine with Docker Desktop + Azure CLI (`az login`).

1. Get names from Terraform (from `infrastructure/terraform` with staging backend selected):

   ```powershell
   terraform output
   ```

   You need `acr_name`, `acr_login_server`, `web_app_name`, `resource_group_name`.

2. From the **repo root** (PowerShell):

   ```powershell
   az acr login --name lfleetstagingacr

   docker build -t lfleetstagingacr.azurecr.io/liberationfleet:latest -f LiberationFleet.Server/Dockerfile .

   docker push lfleetstagingacr.azurecr.io/liberationfleet:latest

   az webapp config container set `
     --name app-lfleet-staging `
     --resource-group rg-lfleet-staging `
     --docker-custom-image-name lfleetstagingacr.azurecr.io/liberationfleet:latest `
     --docker-registry-server-url https://lfleetstagingacr.azurecr.io

   az webapp restart --name app-lfleet-staging --resource-group rg-lfleet-staging
   ```

   Replace names if your `terraform output` differs.

3. Wait 1–3 minutes → open `https://app-lfleet-staging.azurewebsites.net/`.

App Service pulls from ACR using its managed identity (Terraform configured this). If pull fails, check ACR → **Access control** that the Web App’s identity can **AcrPull**.

---

## Step 9 — Verify staging

1. Open the staging URL: `terraform output app_public_url`, or `https://app-lfleet-staging.azurewebsites.net/`.
2. Work the checklist in the browser (and DevTools where noted):

| Check | How |
|-------|-----|
| SPA loads | Home page renders; not Azure “Application Error” / Docker default page |
| Register + login | Create a test user; confirm you land in the app |
| SignalR / notifications | DevTools → **Network** → filter **WS**; after login you should see a WebSocket to `/hubs/...` that stays connected |
| Crew chat | Create/open a crew → open a chat → send a message |
| Database | If login/SQL errors: Portal → Key Vault `ConnectionStrings-DefaultConnection`; SQL server firewall allows Azure services / your IP |
| Donations (optional) | Only after Stripe test keys — [DONATION-SETUP.md](./DONATION-SETUP.md) Part C → `/app/donate` |
| Voice (optional) | Only after LiveKit Cloud + Key Vault keys — [LIVEKIT-SETUP.md](./LIVEKIT-SETUP.md) Path B |

**If the container will not start:** Portal → `app-lfleet-staging` → **Log stream** (or **Diagnose and solve problems**). Common causes: empty ACR (redo Step 8), bad Key Vault reference, SQL connection string.

---

## Step 10 — Custom domain + TLS

Do this when you own a domain (e.g. `liberationfleet.org`) and want a branded URL. Recommended before **production** Stripe live + store listings. Staging can stay on `*.azurewebsites.net`.

### 10.1 Add the hostname on App Service

1. Portal → the Web App that should own the domain (usually **production** after Step 11; or staging if you want `staging.yourdomain.org`).
2. Left menu → **Settings** → **Custom domains** → **Add custom domain**.
3. Domain provider: **All other domain services** (unless you bought the domain in Azure).
4. Enter hostname, e.g. `liberationfleet.org` or `www.liberationfleet.org` or `app.liberationfleet.org`.
5. Azure shows DNS records to create. Leave this tab open.

### 10.2 Create DNS at your registrar

At Cloudflare / Namecheap / etc., add exactly what Azure shows. Typical patterns:

| You want | Common DNS |
|----------|------------|
| `www.liberationfleet.org` | **CNAME** `www` → `app-lfleet-production.azurewebsites.net` (use your real default hostname) |
| Apex `liberationfleet.org` | Often **A** record to App Service IPs **plus** a **TXT** validation record Azure displays |

Save DNS. Propagation can take minutes to hours. In Azure, click **Validate** until it succeeds → **Add**.

### 10.3 Free TLS certificate

1. Still under **Custom domains** → for the new domain → **Add binding** (or **Certificate**).
2. Certificate type: **App Service Managed Certificate** (free) → create / validate.
3. TLS/SSL binding: SNI SSL → save.
4. Open `https://your.domain` and confirm the padlock (no cert warning).

### 10.4 Point the app at the new origin

App settings use the **full origin** including `https://`, no trailing slash — e.g. `https://liberationfleet.org` (not bare `liberationfleet.org`).

1. Web App → **Environment variables**:
   - Set `Stripe__PublicAppBaseUrl` = `https://your.domain`
   - Add `Cors__AllowedOrigins__5` = `https://your.domain` (use `__6` if you also serve `www`)
2. Optional Terraform: set `custom_domain_url = "https://your.domain"` in that env’s `.tfvars` and re-apply so outputs stay consistent.
3. Stripe Dashboard → webhook / Event destination URL → `https://your.domain/api/donations/stripe/webhook` ([DONATION-SETUP.md](./DONATION-SETUP.md)).
4. **Restart** the Web App.
5. Confirm the site loads on the custom domain.

---

## Step 11 — Production

Do this only when staging (Steps 6–9) is healthy and you are ready for a separate production environment.

### 11.1 Apply production Terraform

1. Edit `infrastructure/terraform/environments/production.tfvars` (SKUs, region, optional `livekit_host` / `custom_domain_url`). Prefer stronger SQL + backups for prod.
2. Ensure `environments/production.backend.hcl` exists (from Step 5).
3. PowerShell:

   ```powershell
   cd infrastructure\terraform
   terraform init -reconfigure -backend-config="environments/production.backend.hcl"
   terraform plan -var-file="environments/production.tfvars"
   terraform apply -var-file="environments/production.tfvars"
   ```

4. Capture outputs: `terraform output` → note `resource_group_name`, `web_app_name`, `acr_name`, `acr_login_server`, `key_vault_name`, `app_public_url`.

### 11.2 Variable group `liberationfleet-production`

1. ADO → **Pipelines** → **Library** → **+ Variable group**.
2. Name: exactly `liberationfleet-production`.
3. Add the **same variable names** as staging (§6.4), with **production** output values:

| Name | Value |
|------|--------|
| `ENVIRONMENT` | `production` |
| `AZURE_RESOURCE_GROUP` | production `resource_group_name` |
| `WEB_APP_NAME` | production `web_app_name` |
| `ACR_NAME` | production `acr_name` |
| `ACR_LOGIN_SERVER` | production `acr_login_server` |

4. **Save** → **Pipeline permissions** → allow your LiberationFleet pipeline.

### 11.3 Secrets (production Key Vault only)

Terraform created production Key Vault (typically **`lfleetproductionkv`**). This is **not** `lfleetstagingkv`.

1. Portal → production Key Vault → **Secrets**.
2. For each secret below: open → **New version** → paste → Create.

| Secret | Production value |
|--------|------------------|
| `Stripe-SecretKey` | **Live** `sk_live_…` — [DONATION-SETUP.md](./DONATION-SETUP.md) **Part D** |
| `Stripe-WebhookSecret` | **Live** `whsec_…` (new Event destination on the production URL) |
| `LiveKit-ApiKey` / `LiveKit-ApiSecret` | Production LiveKit Cloud project — [LIVEKIT-SETUP.md](./LIVEKIT-SETUP.md) Path C |
| `ReportEvidence-VendorApiKey` | Prod vendor key — [REPORT-VENDOR-WEBHOOK.md](./REPORT-VENDOR-WEBHOOK.md) |

3. Production App Service → **Environment variables** → set `Stripe__PublicAppBaseUrl` to the production HTTPS origin (custom domain if you have one).
4. If using LiveKit: set `livekit_host` in `production.tfvars`, re-apply, confirm `LiveKit__Host`.
5. Restart the production Web App.

Do **not** put live Stripe keys into the staging Key Vault.

### 11.4 Deploy production via the pipeline

1. Confirm §11.2 variable group is linked to the pipeline.
2. Push to `master` or **Run pipeline** on `master`.
3. Wait for **Deploy staging** to succeed.
4. When **Deploy production** shows **Waiting for approval**:
   - Open the run → open the approval → review → **Approve** (you configured approvers in Step 4).
5. Wait for production deploy to finish.
6. Open production `app_public_url` and repeat the Step 9 checklist.

**Scale rule:** keep **one** App Service instance until Azure SignalR or a Redis backplane is added (in-process SignalR).

### 11.5 SQL backups

1. Portal → production SQL database → **Settings** → **Backup** (wording varies) / retention.
2. Confirm **Point-in-time restore** is available; configure long-term retention if required for the nonprofit.

---

## Step 12 — Point mobile apps at Azure

1. In the repo, open `liberationfleet.client/src/environments/environment.native.ts`.
2. Set:
   ```ts
   apiBaseUrl: 'https://your-production-host'  // no trailing slash; custom domain or *.azurewebsites.net
   ```
3. Follow [NATIVE-APPS.md](./NATIVE-APPS.md): `npm run cap:sync`, then smoke-test on a device/emulator against that API.
4. Submit stores via [STORE-SUBMISSION.md](./STORE-SUBMISSION.md).

Ensure App Service CORS still includes Capacitor origins (`capacitor://localhost`, etc.) — Terraform sets those by default.

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
| Terraform: Authenticating using the Azure CLI is only supported as a User | Pipeline must use OIDC/`ARM_*` (see `.azure/pipelines/templates/terraform-apply.yml`). Re-run after that template is on `master`. |
| Terraform backend 403 after OIDC fix | Grant the pipeline app **Storage Blob Data Contributor** on tfstate storage (`stlfeet51cwzy` / `rg-lfleet-tfstate`). Wait 1–2 min for RBAC, re-run. |
| Terraform Key Vault secret read 403 | Grant pipeline app **Key Vault Secrets Officer** on that vault. Wait 1–2 min, re-run. |
| Terraform roleAssignments delete/write 403 | Grant pipeline app **User Access Administrator** on the subscription (or RG). |
| Terraform backend errors | Wrong `TF_STATE_*` variable group values; not logged in (`az login`); storage firewall |
| Container pull fails | ACR permissions for App Service managed identity; image tag missing |
| 502/503 after deploy | Check Log stream; confirm migrations / connection string |
| CORS errors from Capacitor | Keep Capacitor origins; add custom domain origin |
| Stripe totals stay $0 | Webhook secret + destination URL wrong |
| Voice join fails | `LiveKit__Host` must be `wss://…`; Key Vault API key/secret set |
