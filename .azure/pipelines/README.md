# Azure DevOps CI/CD for LiberationFleet

Pipeline YAML: [`/azure-pipelines.yml`](../../azure-pipelines.yml)  
End-to-end go-live (accounts → first URL → production): [`docs/AZURE-GO-LIVE.md`](../../docs/AZURE-GO-LIVE.md)

## What the pipeline does

| Stage | When | What |
|-------|------|------|
| **Build** | `master` (non-PR) | Restore, build, test, Docker build (no push yet) |
| **Deploy staging** | `master`, and `STAGING_ENABLED` ≠ `false` | Terraform apply staging + push image to ACR + update App Service |
| **Deploy production** | **Only** when you **Run pipeline** and check **Deploy to production** | Same for production; still waits on Environment approval |
| **PR gate** | Pull requests | Agentless acknowledge (real PR checks are GitHub Actions) |

**Important:** a normal push to `master` never deploys production. Production is opt-in on a manual run, then Environment approval.

## One-time setup checklist

Follow the detailed UI steps in [AZURE-GO-LIVE.md](../../docs/AZURE-GO-LIVE.md) Steps 2–6 and 11. Summary:

### 1. Service connection

| Field | Value |
|-------|--------|
| Type | Azure Resource Manager |
| Auth | Workload identity federation (preferred) |
| Name | **`azure-liberationfleet`** (exact) |

### 2. Environments

| Name | Approvals |
|------|-----------|
| `staging` | None |
| `production` | Required (Approvals and checks) |

### 3. Variable groups

Create under **Pipelines → Library**.

#### `liberationfleet-shared`

| Variable | Source |
|----------|--------|
| `TF_STATE_RG` | Bootstrap terraform output `resource_group_name` |
| `TF_STATE_STORAGE` | Bootstrap output `storage_account_name` |
| `TF_STATE_CONTAINER` | `tfstate` |

#### `liberationfleet-staging`

| Variable | Source (staging `terraform output`) |
|----------|-------------------------------------|
| `ENVIRONMENT` | `staging` |
| `AZURE_RESOURCE_GROUP` | `resource_group_name` |
| `WEB_APP_NAME` | `web_app_name` |
| `ACR_NAME` | `acr_name` |
| `ACR_LOGIN_SERVER` | `acr_login_server` |
| `STAGING_ENABLED` | `true` normally; set `false` when staging is destroyed/paused so pushes do not recreate it |

#### `liberationfleet-production`

Same keys as staging **except** do not add `STAGING_ENABLED`. Values from **production** terraform outputs. Set `ENVIRONMENT` = `production`.

### 4. Create the pipeline

1. **Pipelines → New pipeline** → select repo.
2. **Existing Azure Pipelines YAML file** → `/azure-pipelines.yml`.
3. Link variable groups if the UI asks.
4. First run on `master`: approve any “authorize resource” prompts for the service connection and environments.

## Deploy workflow (after go-live)

| Goal | Action |
|------|--------|
| Update staging | Push/merge to `master` (with `STAGING_ENABLED=true`) |
| Ship production | **Run pipeline** on `master` → check **Deploy to production** → Approve Environment |
| Pause staging (save cost) | See [AZURE-GO-LIVE Step 13](../../docs/AZURE-GO-LIVE.md#step-13--pause-staging-to-save-money-recommended-after-prod-is-healthy) |
| Prod-only while staging destroyed | `STAGING_ENABLED=false` + Run pipeline with **Deploy to production** checked |

## First-run order (infra before green deploy)

1. Bootstrap state — `infrastructure/terraform/bootstrap` ([AZURE-GO-LIVE Step 5](../../docs/AZURE-GO-LIVE.md#step-5--bootstrap-terraform-remote-state-one-time)).
2. Local `terraform apply` for staging once (creates ACR + App Service).
3. Fill `liberationfleet-shared` + `liberationfleet-staging` (include `STAGING_ENABLED=true`).
4. Create pipeline from `azure-pipelines.yml`.
5. Set Stripe / LiveKit / report secrets in Key Vault.
6. Run / push `master` for container deploy to staging.
7. Production: apply prod Terraform, fill `liberationfleet-production`, then **Run pipeline** with **Deploy to production** and Approve.

## Templates

| File | Purpose |
|------|---------|
| `templates/terraform-apply.yml` | Init / plan / optional apply with Azure RM service connection |
| `templates/docker-push-deploy.yml` | ACR login, push, App Service container update, restart |

## Secrets policy

Do **not** put Stripe/LiveKit keys in pipeline variables. Store them in **Key Vault**; App Service references them via Terraform-managed settings.
