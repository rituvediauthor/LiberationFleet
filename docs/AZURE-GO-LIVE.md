# Azure go-live (web API + SPA)

Hosts the combined ASP.NET + Angular container used by **web** and by **native apps** as the API backend.

| Doc | Role |
|-----|------|
| This file | End-to-end Azure account → first staging URL → production |
| [`infrastructure/terraform/README.md`](../infrastructure/terraform/README.md) | Terraform modules & outputs reference |
| [`.azure/pipelines/README.md`](../.azure/pipelines/README.md) | Variable groups & pipeline wiring |
| [`LAUNCH-CHECKLIST.md`](./LAUNCH-CHECKLIST.md) | Master list (legal, stores, third parties) |

**What you will have when finished:** staging URL first (Steps 1–9), then a separate production environment (Step 11+) with **manual** production deploys (push to `master` never ships production by itself). After go-live you can **pause/destroy staging** so you mostly pay for production only (Step 13).

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
| `STAGING_ENABLED` | `true` |

`STAGING_ENABLED` controls whether pushes to `master` deploy staging. Set it to `false` later when you pause/destroy staging (Step 13) so the pipeline does not recreate it.

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
   2. Add the same names as staging (`ENVIRONMENT`, `AZURE_RESOURCE_GROUP`, `WEB_APP_NAME`, `ACR_NAME`, `ACR_LOGIN_SERVER`). Do **not** add `STAGING_ENABLED` here.
   3. For now you can copy staging values or use placeholders (e.g. `ENVIRONMENT` = `production`, others = `pending`). Real production outputs come in Step 11.
   4. **Save** → **Pipeline permissions** → allow your LiberationFleet pipeline (or authorize when the run prompts).

   If you skip this group, the run fails immediately with: *Variable group liberationfleet-production could not be found*.

   Also confirm `liberationfleet-staging` has **`STAGING_ENABLED` = `true`** (Step 6.4). Without it, staging still deploys (missing/empty is treated as enabled).

#### A.3 Run a staging deploy

**Automatic:** merge or push a commit to **`master`** (docs-only changes under `docs/` are excluded by the YAML and will **not** trigger).

**Manual (recommended for first deploy):**

1. **Pipelines** → your pipeline → **Run pipeline**.
2. Branch: `master` → **Run**.
3. Open the run. Watch stages in order: **Build and test** → **Deploy staging**.
4. If **Deploy staging** asks to use environment `staging` or a resource, **Permit**.
5. Wait until **Deploy staging** is green (often 10–20+ minutes the first time: build, Terraform, Docker push, App Service restart).

#### A.4 Production does **not** run on a normal push

Production is **opt-in**. A normal push / Run pipeline leaves **Deploy to production** unchecked (default), so only **Build** + **Deploy staging** run. You will deploy production later in **Step 11.6** when you are ready.

If you accidentally check **Deploy to production** before Step 11 is done: **Reject** the Environment approval (or cancel the run). Staging is unaffected.

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

## Step 10 — Custom domain + TLS (optional; usually production)

Do this when you own a domain and want a branded URL. **Staging can stay on `*.azurewebsites.net`.**

For **production** go-live, the same steps are repeated in full inside **Step 11.5** so you do not need to flip back here. Use this step if you want a branded **staging** host (e.g. `staging.yourdomain.org`) before production exists.

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

## Step 11 — Production (full go-live)

Do this only when staging (Steps 6–9) is healthy and you are ready for a **separate** production stack.

**How deploy works after this step**

| Action | What happens |
|--------|----------------|
| Push / merge to `master` | Build + deploy **staging** only (if `STAGING_ENABLED` is not `false`) |
| Manual **Run pipeline** with **Deploy to production** checked | Build → staging (if enabled) → **production** (still needs Environment approval from Step 4) |
| Manual run with staging paused (`STAGING_ENABLED=false`) + Deploy to production checked | Build → skip staging → production only |

Production is **never** deployed by a plain push. You choose when.

**Self-contained:** everything you need for production (infra, secrets, Stripe live, LiveKit, custom domain, first deploy, verify) is in this step and Step 12–13. You should not need to re-read Steps 1–10 except for lookup.

---

### 11.0 Before you start (checklist)

Confirm each item. Fix anything missing first.

| # | Check | Where / how |
|---|--------|-------------|
| 1 | Staging site works | Browser opens staging URL; login works (Step 9) |
| 2 | `production.backend.hcl` exists on your machine | `infrastructure/terraform/environments/production.backend.hcl` (created in Step 5.3; **gitignored**) |
| 3 | Variable group stub `liberationfleet-production` exists | ADO → Pipelines → Library (created in Step 8 A.2) |
| 4 | Environment `production` has an **Approvals** check | ADO → Pipelines → Environments → `production` (Step 4.2) |
| 5 | You are logged into Azure CLI on this machine | `az account show` prints your subscription |
| 6 | Same region plan as staging | Staging uses `location` in `staging.tfvars` (yours is likely `westus`). Production should match unless you intentionally split regions. |

---

### 11.1 Review `production.tfvars` (usually little or no editing)

**You are not starting from a blank file.** The repo already has:

`infrastructure/terraform/environments/production.tfvars`

Open it in your editor. For a first production go-live on a tight budget, the **defaults already in the file are intentional** (B1 App Service, S0 SQL, Basic ACR, purge protection on).

#### 11.1.1 What each setting means — edit only if you need to

| Line / setting | What it does | First go-live guidance |
|----------------|--------------|-------------------------|
| `project_name = "lfleet"` | Prefix for Azure resource names | **Do not change** (would rename everything) |
| `environment = "production"` | Marks this stack as production | **Do not change** |
| `location = "westus"` | Azure region for all prod resources | **Keep the same region as staging** unless you know you need otherwise. If staging is `westus` / `westus2`, match it. |
| `app_service_sku = "B1"` | App Service plan size (main compute cost) | Leave `B1` until traffic requires more |
| `sql_sku_name = "S0"` | Azure SQL tier | Leave `S0` for now (always-on DTU; predictable) |
| `sql_max_size_gb = 10` | Max DB size | Leave unless you expect large media metadata growth |
| `acr_sku = "Basic"` | Container registry tier | Leave `Basic` |
| `key_vault_purge_protection_enabled = true` | Soft-deleted Key Vault cannot be instantly purged | **Leave `true` for production** |
| `log_retention_days = 30` | App Insights / Log Analytics retention | Leave `30` unless compliance needs longer |
| `# custom_domain_url = ...` | Commented optional public URL | Leave commented until DNS is ready (handled in §11.5) |
| `# livekit_host = ...` | Commented LiveKit WSS URL | Leave commented until you do voice in §11.4 |
| `tags = { ... }` | Cost/org tags | Optional; leave as-is |

**Also optional (copy pattern from staging if you use SSMS):** add `sql_firewall_rules` with your home/office public IP so you can connect to production SQL from your PC. Example (use **your** IP, not this sample):

```hcl
sql_firewall_rules = [
  {
    name             = "Home"
    start_ip_address = "YOUR.PUBLIC.IP.HERE"
    end_ip_address   = "YOUR.PUBLIC.IP.HERE"
  }
]
```

To find your public IP: open [https://ifconfig.me](https://ifconfig.me) in a browser.

**Bottom line for 11.1.1:** if `location` already matches staging and you are fine with B1/S0, **save nothing — go to 11.2**. You only edit this file when region, SKU, LiveKit host, custom domain URL, or SQL firewall need to change.

---

### 11.2 Apply production Terraform (creates empty Azure resources)

This creates a **second** full stack: resource group, App Service, ACR, SQL, Key Vault, App Insights, etc. It does **not** copy staging data. It does **not** deploy your app image yet (that is §11.6).

1. Open PowerShell.
2. Confirm Azure login:

   ```powershell
   az account show
   ```

   If that errors, run `az login` and pick the same subscription you used for staging.

3. Go to the Terraform folder:

   ```powershell
   cd <path-to-your-clone>\infrastructure\terraform
   ```

4. Point Terraform at the **production** state file (not staging):

   ```powershell
   terraform init -reconfigure -backend-config="environments/production.backend.hcl"
   ```

   You should see init succeed. The important part is backend key `production.terraform.tfstate` (inside that `.hcl` file).

5. Preview what will be created (no changes yet):

   ```powershell
   terraform plan -var-file="environments/production.tfvars"
   ```

   Expect many resources to **add** (App Service, SQL, Key Vault, ACR, …). If the plan looks wrong (destroying staging names, wrong region), **stop** and check that you used `production.backend.hcl` and `production.tfvars`.

6. Apply:

   ```powershell
   terraform apply -var-file="environments/production.tfvars"
   ```

   Type `yes` when prompted. First apply often takes **10–20+ minutes** (SQL is slow).

7. When it finishes, capture outputs:

   ```powershell
   terraform output
   ```

   Write these down (you need them in the next subsection):

| Output | Typical value | Used for |
|--------|---------------|----------|
| `resource_group_name` | `rg-lfleet-production` | ADO variable group |
| `web_app_name` | `app-lfleet-production` | ADO + portal |
| `acr_name` | `lfleetproductionacr` | ADO + Docker |
| `acr_login_server` | `lfleetproductionacr.azurecr.io` | ADO |
| `key_vault_name` | `lfleetproductionkv` | Secrets |
| `app_public_url` | `https://app-lfleet-production.azurewebsites.net` | Browser, Stripe, CORS |
| `web_app_default_hostname` | `app-lfleet-production.azurewebsites.net` | DNS CNAME target |

#### If apply fails

Same class of fixes as staging (Step 6.2): wrong region / SQL provisioning / App Service quota / Key Vault name soft-delete. For Key Vault name collision:

```powershell
az keyvault list-deleted -o table
az keyvault purge --name lfleetproductionkv
```

Then re-run `terraform apply -var-file="environments/production.tfvars"`.

#### If apply fails: Key Vault secret 403 (`ForbiddenByRbac`)

Terraform created `lfleetproductionkv` but your user cannot write secrets yet. Grant yourself **Key Vault Secrets Officer**, wait ~1–2 minutes, re-apply (safe to re-run; it continues from state):

```powershell
$oid = az ad signed-in-user show --query id -o tsv
az role assignment create `
  --role "Key Vault Secrets Officer" `
  --assignee-object-id $oid `
  --assignee-principal-type User `
  --scope "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/rg-lfleet-production/providers/Microsoft.KeyVault/vaults/lfleetproductionkv"

Start-Sleep -Seconds 90
terraform apply -var-file="environments/production.tfvars"
```

(This is the same role you already use on staging — production is a **new** vault, so it needs its own assignment.)

---

### 11.3 Fill variable group `liberationfleet-production` with real values

You may already have a **stub** group from Step 8. Now replace placeholders with **production** terraform outputs (not staging names).

1. Azure DevOps → **Pipelines** → **Library** → open **`liberationfleet-production`**.
2. Set each variable:

| Name | Value |
|------|--------|
| `ENVIRONMENT` | `production` |
| `AZURE_RESOURCE_GROUP` | production `resource_group_name` (e.g. `rg-lfleet-production`) |
| `WEB_APP_NAME` | production `web_app_name` (e.g. `app-lfleet-production`) |
| `ACR_NAME` | production `acr_name` |
| `ACR_LOGIN_SERVER` | production `acr_login_server` |

3. **Save**.
4. Tab **Pipeline permissions** → ensure your LiberationFleet pipeline is allowed (or authorize on first prod run).

Double-check you did **not** paste staging resource names (`rg-lfleet-staging`, `app-lfleet-staging`, `lfleetstagingacr`, …).

---

### 11.4 Production Key Vault secrets + app settings

Terraform created production Key Vault (typically **`lfleetproductionkv`**). This is a **different vault** from staging (`lfleetstagingkv`).

#### 11.4.1 Open the production vault

1. Portal → search for the `key_vault_name` from §11.2 outputs.
2. If access denied: **Access control (IAM)** → add your user as **Key Vault Secrets Officer** → wait 1–2 minutes.
3. Left menu → **Secrets**.

#### 11.4.2 Set product secrets (New version on each)

For each row: open the secret → **New version** → paste → **Create**.

| Secret name | Required when | What to paste |
|-------------|---------------|---------------|
| `Stripe-SecretKey` | Before live donations | Stripe **Live** secret key `sk_live_…` (stay on your live business account — not a Sandbox → Developers → API keys). Full walkthrough: [DONATION-SETUP.md](./DONATION-SETUP.md) Part D |
| `Stripe-WebhookSecret` | Before live donation totals update | Live webhook signing secret `whsec_…` from a **new** Event destination aimed at the **production** URL (see §11.4.4) |
| `LiveKit-ApiKey` | Before production voice | API key from a **production** LiveKit Cloud project (prefer separate from staging) — [LIVEKIT-SETUP.md](./LIVEKIT-SETUP.md) Path C |
| `LiveKit-ApiSecret` | Before production voice | Matching API secret |
| `ReportEvidence-VendorApiKey` | Before vendor ops API in prod | Long random string you generate — **optional on day one** (you can be the reviewer). See [REPORT-VENDOR-WEBHOOK.md](./REPORT-VENDOR-WEBHOOK.md) Path A |

**Do not overwrite casually** (Terraform-managed): `ConnectionStrings-DefaultConnection`, `Jwt-SecretKey`, `ReportEvidence-AesKeyBase64`, deep-freeze storage connection.

**Do not** put `sk_live_…` into the staging vault. **Do not** put `sk_test_…` into the production vault.

If donations or voice are not ready on day one, you can leave Stripe/LiveKit placeholders and continue; those features will fail until filled.

#### 11.4.3 Production App Service environment variables

These live on the **Web App**, not in Key Vault. They are plain app settings (environment variables). The double underscore `__` is how .NET maps nested config (e.g. `Stripe:PublicAppBaseUrl`).

1. Open [portal.azure.com](https://portal.azure.com) on the subscription that hosts production (**LF_sub** if that is yours).
2. Resource group **`rg-lfleet-production`** → Web App **`app-lfleet-production`**.
3. Left menu → **Settings** → **Environment variables**  
   (older UI: **Configuration** → **Application settings**).
4. Find each name below (use the search box). If a name is missing, **+ Add** it.
5. Confirm / set:

| Setting | Value |
|---------|--------|
| `Stripe__PublicAppBaseUrl` | Production HTTPS origin, **no trailing slash**. Prefer `terraform output app_public_url` (e.g. `https://liberationfleet.org` if `custom_domain_url` is set; otherwise `https://app-lfleet-production.azurewebsites.net`) |
| `Cors__AllowedOrigins__0` | Same origin as `Stripe__PublicAppBaseUrl` (Terraform usually set this already) |

6. Click **Apply** / **Save**.
7. You can **Restart** now, or wait until after Key Vault Stripe/LiveKit secrets and the first image deploy (§11.6), then restart once.

#### 11.4.4 Stripe live webhook (when enabling live donations)

1. Stripe Dashboard → stay in your **live** business account (not a Sandbox).
2. **Developers** → **Webhooks** / Event destinations → **Add**.
3. Endpoint URL: `https://YOUR-PRODUCTION-ORIGIN/api/donations/stripe/webhook`  
   - Without custom domain: `https://app-lfleet-production.azurewebsites.net/api/donations/stripe/webhook`  
   - With custom domain (after §11.5): `https://your.domain/api/donations/stripe/webhook`
4. Select the events required by [DONATION-SETUP.md](./DONATION-SETUP.md) Part D.
5. Copy the signing secret → production Key Vault `Stripe-WebhookSecret` → New version.
6. Restart the production Web App after the secret is saved.

#### 11.4.5 LiveKit host on production (when enabling voice)

Full click-path for finding keys: [LIVEKIT-SETUP.md](./LIVEKIT-SETUP.md) **Path C.1**. Short version:

1. Open [https://cloud.livekit.io](https://cloud.livekit.io) → create/select a **production** project (separate from staging).
2. **Settings** → **Keys** → **click the key row** → copy WebSocket URL (`wss://…`), API Key, and API Secret (Reveal if needed).
3. Edit `production.tfvars` and set (uncomment if needed):

   ```hcl
   livekit_host = "wss://your-production-project.livekit.cloud"
   ```

4. From `infrastructure/terraform` (production backend still selected from §11.2):

   ```powershell
   terraform apply -var-file="environments/production.tfvars"
   ```

5. Portal → **`lfleetproductionkv`** → Secrets → **`LiveKit-ApiKey`** / **`LiveKit-ApiSecret`** → New versions → paste.
6. Portal → **`app-lfleet-production`** → Environment variables → confirm **`LiveKit__Host`** matches the `wss://` URL → **Restart** Web App.

---

### 11.5 Custom domain + TLS for production (optional but recommended)

Skip this subsection if you will launch on `*.azurewebsites.net` first. You can add a domain later and then update Stripe/CORS.

If you own a domain (e.g. `liberationfleet.org`) and want production on it:

#### 11.5.1 Add hostname on the production Web App

1. Portal → **`app-lfleet-production`** → **Settings** → **Custom domains** → **Add custom domain**.
2. Domain provider: **All other domain services** (unless the domain was bought in Azure).
3. Enter hostname (`liberationfleet.org`, `www.…`, or `app.…`).
4. Leave the Azure tab open — it shows DNS records to create.

#### 11.5.2 Create DNS at Cloudflare (or your registrar)

Azure shows the exact records. Create them in **Cloudflare → your zone → DNS → Records**.

| You want | Typical Cloudflare record |
|----------|---------------------------|
| `www.liberationfleet.org` | **CNAME** Name `www` → Target `app-lfleet-production.azurewebsites.net` (Proxy status: see note below) |
| Apex `liberationfleet.org` | Follow Azure’s UI: usually a **TXT** for verification **plus** an **A** (or Cloudflare CNAME flattening) to the App Service host/IPs Azure shows |

**Cloudflare proxy (orange cloud) tip:** for first-time **domain validation** and **App Service Managed Certificate**, set the record to **DNS only** (grey cloud). After Azure shows the domain as Secured, you can try turning the proxy back on if you want Cloudflare CDN/WAF — if TLS breaks, leave it DNS-only pointing at Azure.

Save records. Wait a few minutes. In Azure Custom domains → **Validate** until it succeeds → **Add**.

#### 11.5.3 Free TLS certificate

1. Under **Custom domains** → for the new domain → **Add binding** / certificate.
2. Type: **App Service Managed Certificate** (free) → create / validate.
3. TLS/SSL binding: **SNI SSL** → save.
4. Open `https://your.domain` and confirm the padlock (after the app is deployed in §11.6; before deploy you may still see Application Error).

#### 11.5.4 Point the app at the custom origin

Use the full origin including `https://`, **no trailing slash**.

1. Production Web App → **Environment variables**:
   - `Stripe__PublicAppBaseUrl` = `https://your.domain`
   - Add `Cors__AllowedOrigins__5` = `https://your.domain` (use `__6` if you also serve `www`)
2. Optional but recommended — keep Terraform in sync. In `production.tfvars`:

   ```hcl
   custom_domain_url = "https://your.domain"
   ```

   Then:

   ```powershell
   terraform apply -var-file="environments/production.tfvars"
   ```

3. Update the **Stripe live webhook** endpoint (Stripe Dashboard — not Azure):
   1. Open [https://dashboard.stripe.com](https://dashboard.stripe.com).
   2. Stay in your **live** business account (not a Sandbox — if you see **Switch to sandbox**, you are already live).
   3. Left nav → **Developers** → **Webhooks** (or **Event destinations**).
   4. If you already created a live destination for `*.azurewebsites.net`: open it → **Update details** / edit endpoint URL → set  
      `https://liberationfleet.org/api/donations/stripe/webhook` → save.  
      (Signing secret `whsec_…` usually **stays the same** when you only change the URL.)
   5. If you have **no** live destination yet: **Add destination** / **Add endpoint** → URL  
      `https://liberationfleet.org/api/donations/stripe/webhook` → same `checkout.session.*` events as staging → create → copy the new `whsec_…` into Key Vault **`lfleetproductionkv`** → **`Stripe-WebhookSecret`** → New version (see §11.4.4).
   6. Do **not** edit your staging/sandbox webhook.
4. **Restart** the Web App after deploy (and after any new webhook secret).

---

### 11.6 First production deploy (manual — you choose when)

Prerequisites: §11.2–11.3 done. Secrets in §11.4 can be partial if you are only testing the SPA shell first.

1. Merge or ensure the code you want live is on **`master`** (and staging has been tested with that code if staging is still up).
2. Azure DevOps → **Pipelines** → your LiberationFleet pipeline → **Run pipeline**.
3. Branch: **`master`**.
4. Check the box **Deploy to production (after build; only when you are ready)** → **Run**.
5. Watch stages:
   - **Build and test** must go green.
   - **Deploy staging** runs if `STAGING_ENABLED` is not `false`.
   - **Deploy production** starts, then **Waiting for approval**.
6. Open the approval → review → **Approve** (you set approvers in Step 4).
7. Wait until **Deploy production** is green (often 10–20+ minutes the first time).
8. Open production URL (`app_public_url` or custom domain). You should see the SPA, not a permanent Application Error.

**Scale rule:** keep **one** App Service instance until Azure SignalR or a Redis backplane is added (in-process SignalR).

#### Manual Docker alternative (only if the pipeline is unavailable)

Same idea as staging Option B, but use **production** ACR / app / RG names from `terraform output` after the production backend init. Prefer the pipeline.

---

### 11.7 SQL backups (production)

1. Portal → production SQL **database** (inside the production SQL server) → look for **Backup** / **Retention** / **Point-in-time restore** settings (wording varies).
2. Confirm point-in-time restore is available.
3. Optionally configure long-term retention if the nonprofit requires it.

---

## Step 12 — Verify production

Work this checklist on the **production** URL (custom domain or `https://app-lfleet-production.azurewebsites.net/`).

| Check | How |
|-------|-----|
| SPA loads | Home page renders; not Azure “Application Error” |
| Register + login | Create a real/admin test user; land in the app |
| SignalR | DevTools → Network → WS → `/hubs/...` connected after login |
| Crew chat | Send a message in a crew chat |
| Donations (if live keys set) | Small real charge only if you intend to; otherwise leave until Stripe Part D is done |
| Voice (if LiveKit set) | Join a voice room |
| CORS / native later | Capacitor origins should already be present from Terraform |

**If the container will not start:** Portal → `app-lfleet-production` → **Log stream**. Common causes: empty ACR (re-run §11.6), bad Key Vault reference, SQL connection string.

---

## Step 13 — Pause staging to save money (recommended after prod is healthy)

Goal: run **production only** most of the time. Spin staging up only when you need to test a major fix or feature.

Your staging SQL SKU is **S0** (always billed while the database exists). **Stopping the Web App alone is not enough** to stop most staging cost — you should **destroy** the staging stack when idle.

### 13.1 Soft pause (quick, partial savings)

Use only for a short break (hours/days):

1. Portal → `app-lfleet-staging` → **Stop**.
2. SQL S0 and ACR **keep billing**.

Prefer §13.2 for real savings.

### 13.2 Hard pause — destroy staging (recommended)

This deletes staging Azure resources but **keeps Terraform state** in the bootstrap storage account so you can recreate later.

1. In ADO → **Pipelines** → **Library** → **`liberationfleet-staging`** → set **`STAGING_ENABLED`** = **`false`** → **Save**.  
   This stops `master` pushes from trying to recreate staging.
2. PowerShell — switch Terraform to the **staging** backend, then destroy:

   ```powershell
   cd <path-to-your-clone>\infrastructure\terraform
   terraform init -reconfigure -backend-config="environments/staging.backend.hcl"
   terraform destroy -var-file="environments/staging.tfvars"
   ```

   Type `yes` when prompted. Wait until finished.
3. Confirm in Portal: resource group `rg-lfleet-staging` is gone (or empty).
4. Leave production alone. Bootstrap / tfstate storage (`rg-lfleet-tfstate`) must **stay**.

**Notes**

- Staging Key Vault may soft-delete. If recreate fails later with `VaultAlreadyExists`, purge:  
  `az keyvault purge --name lfleetstagingkv`
- Staging **data** (users, messages) in that SQL database is deleted with destroy. That is expected for a throwaway test environment.
- Production is untouched.

### 13.3 Resume staging when you need to test

1. ADO Library → **`liberationfleet-staging`** → set **`STAGING_ENABLED`** = **`true`** → **Save**.
2. Recreate infra:

   ```powershell
   cd <path-to-your-clone>\infrastructure\terraform
   terraform init -reconfigure -backend-config="environments/staging.backend.hcl"
   terraform apply -var-file="environments/staging.tfvars"
   ```

3. Re-set staging Key Vault secrets if they were wiped (Stripe **test** keys, LiveKit, etc.) — same as Step 7.
4. Confirm variable group `liberationfleet-staging` still has the correct ACR / app names from `terraform output` (names are usually stable).
5. Deploy an image: push to `master`, or **Run pipeline** (leave **Deploy to production** unchecked).
6. Verify with Step 9.
7. When finished testing: deploy production if needed (§11.6), then hard-pause staging again (§13.2).

### 13.4 Day-to-day deploy workflow (after go-live)

| You want to… | Do this |
|--------------|---------|
| Ship a small fix you already trust | Prefer testing on a branch + PR; then merge to `master`. If staging is **up**, it auto-deploys for a smoke test. When happy: **Run pipeline** on `master` with **Deploy to production** checked → Approve. |
| Build a major feature | Resume staging (§13.3) → develop/test there → production deploy when ready → pause staging (§13.2). |
| Deploy production while staging is destroyed | Keep `STAGING_ENABLED=false`. **Run pipeline** → check **Deploy to production** → Approve. Staging stage is skipped. |

---

## Step 14 — GitHub repo access (clone OK, strangers cannot push)

Your remote is `https://github.com/rituvediauthor/LiberationFleet.git`.

**What the public API shows today:** the repo is **public** (`visibility: public`). That means:

| Anyone on the internet | Can they? |
|------------------------|-----------|
| **Clone / fork / download** the code | **Yes** (what you want) |
| **Push commits** directly to your repo | **No** — not unless you add them as a collaborator with Write (or higher) |
| **Open a pull request** from a fork | **Yes** — you choose whether to merge |

So “public” ≠ “anyone can commit to my main branch.” Commits to `rituvediauthor/LiberationFleet` require authenticated **Write** access.

### 14.1 Confirm nobody unexpected can push

1. Open [https://github.com/rituvediauthor/LiberationFleet/settings/access](https://github.com/rituvediauthor/LiberationFleet/settings/access) (repo **Settings** → **Collaborators and teams**).
2. Remove anyone you do not recognize / do not want to have Write.
3. Optional: **Settings** → **Branches** → add a branch protection rule on `master` (require PR, disallow force push). Useful even as a solo owner.

### 14.2 If you ever want the code private

**Settings** → **General** → **Danger Zone** → **Change repository visibility** → Private. Then only people you invite can clone. You said you are fine with public clones — leaving it **public** is correct for that goal.

---

## Step 15 — Point mobile apps at Azure production

1. Open `liberationfleet.client/src/environments/environment.native.ts`.
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
| Staging cost | Prefer destroy (§13.2) over Stop when idle; S0 SQL bills while it exists |
| SQL prod backups | Enable PITR / LTR (§11.7) |
| Deep freeze | Confirm `MediaDeepFreeze__Provider=azure` in prod — [MEDIA-DEEP-FREEZE.md](./MEDIA-DEEP-FREEZE.md) |
| Cost | ACR Basic, App Service B1, SQL S0 — review Cost Management monthly |
| Production deploys | Always manual Run + **Deploy to production** + Environment approval |

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
| Push to master tries to recreate staging after destroy | Set `STAGING_ENABLED`=`false` in variable group **`liberationfleet-staging`**, then re-run. |
| Production never deploys | Expected on push. Use **Run pipeline** and check **Deploy to production**, then Approve. |
