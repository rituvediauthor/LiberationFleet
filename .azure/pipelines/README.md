# Azure DevOps CI/CD for LiberationFleet

Pipeline YAML: [`/azure-pipelines.yml`](../../azure-pipelines.yml)  
End-to-end go-live (accounts → first URL): [`docs/AZURE-GO-LIVE.md`](../../docs/AZURE-GO-LIVE.md)

## What the pipeline does

| Stage | When | What |
|-------|------|------|
| **Build** | PRs + `main` | Restore, build, test, Docker build (no push on PR) |
| **Terraform plan (PR)** | Pull requests | `terraform plan` against staging backend |
| **Deploy staging** | `main` | Terraform apply staging + push image to ACR + update App Service |
| **Deploy production** | `main` after staging | Same for production; waits on Environment approval |

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

#### `liberationfleet-production`

Same keys as staging; values from **production** terraform outputs. Set `ENVIRONMENT` = `production`.

### 4. Create the pipeline

1. **Pipelines → New pipeline** → select repo.
2. **Existing Azure Pipelines YAML file** → `/azure-pipelines.yml`.
3. Link variable groups if the UI asks.
4. First run on `main`: approve any “authorize resource” prompts for the service connection and environments.

## First-run order (infra before green deploy)

1. Bootstrap state — `infrastructure/terraform/bootstrap` ([AZURE-GO-LIVE Step 5](../../docs/AZURE-GO-LIVE.md#step-5--bootstrap-terraform-remote-state-one-time)).
2. Local `terraform apply` for staging once (creates ACR + App Service).
3. Fill `liberationfleet-shared` + `liberationfleet-staging`.
4. Create pipeline from `azure-pipelines.yml`.
5. Set Stripe / LiveKit / report secrets in Key Vault.
6. Run / push `main` for container deploy.
7. Production: apply prod Terraform, fill `liberationfleet-production`, approve Environment.

## Templates

| File | Purpose |
|------|---------|
| `templates/terraform-apply.yml` | Init / plan / optional apply with Azure RM service connection |
| `templates/docker-push-deploy.yml` | ACR login, push, App Service container update, restart |

## Secrets policy

Do **not** put Stripe/LiveKit keys in pipeline variables. Store them in **Key Vault**; App Service references them via Terraform-managed settings.
