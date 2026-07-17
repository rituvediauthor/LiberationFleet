# Azure DevOps CI/CD for LiberationFleet
#
# Pipeline file: /azure-pipelines.yml
# Infrastructure: /infrastructure/terraform
#
# ## Service connections
# - `azure-liberationfleet` — Azure Resource Manager (Workload identity federation preferred)
#
# ## Variable groups
# ### liberationfleet-shared
# | Name | Example |
# |------|---------|
# | TF_STATE_RG | rg-lfleet-tfstate |
# | TF_STATE_STORAGE | stlfeetxxxxxx |
# | TF_STATE_CONTAINER | tfstate |
#
# ### liberationfleet-staging / liberationfleet-production
# Populate after first `terraform apply` (outputs):
# | Name | Source |
# |------|--------|
# | AZURE_RESOURCE_GROUP | resource_group_name |
# | WEB_APP_NAME | web_app_name |
# | ACR_NAME | acr_name |
# | ACR_LOGIN_SERVER | acr_login_server |
#
# ## Environments
# Create ADO Environments `staging` and `production`.
# Add **Approvals and checks** on `production` before enabling prod deploys.
#
# ## First-run order
# 1. Bootstrap state: `infrastructure/terraform/bootstrap`
# 2. Apply staging Terraform locally once (creates ACR + App Service)
# 3. Fill variable groups from outputs
# 4. Create pipeline from `azure-pipelines.yml`
# 5. Set Stripe / LiveKit secrets in Key Vault
