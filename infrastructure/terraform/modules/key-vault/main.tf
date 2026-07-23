data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "this" {
  name                       = var.name
  location                   = var.location
  resource_group_name        = var.resource_group_name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7
  purge_protection_enabled   = var.purge_protection_enabled
  rbac_authorization_enabled = true
  tags                       = var.tags

  network_acls {
    default_action = "Allow"
    bypass         = "AzureServices"
  }
}

resource "azurerm_role_assignment" "terraform_secrets_officer" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_role_assignment" "app_secrets_user" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = var.app_principal_id
}

resource "random_password" "jwt_secret" {
  length  = 64
  special = true
}

resource "random_password" "report_evidence_aes_key" {
  length  = 32
  special = false
}

resource "azurerm_key_vault_secret" "jwt_secret" {
  name         = "Jwt-SecretKey"
  value        = random_password.jwt_secret.result
  key_vault_id = azurerm_key_vault.this.id

  depends_on = [azurerm_role_assignment.terraform_secrets_officer]
}

resource "azurerm_key_vault_secret" "sql_connection_string" {
  name         = "ConnectionStrings-DefaultConnection"
  value        = var.sql_connection_string
  key_vault_id = azurerm_key_vault.this.id

  depends_on = [azurerm_role_assignment.terraform_secrets_officer]
}

resource "azurerm_key_vault_secret" "report_evidence_aes_key" {
  name         = "ReportEvidence-AesKeyBase64"
  value        = base64encode(random_password.report_evidence_aes_key.result)
  key_vault_id = azurerm_key_vault.this.id

  depends_on = [azurerm_role_assignment.terraform_secrets_officer]
}

# Placeholders — set real values via pipeline or portal after first apply.
resource "azurerm_key_vault_secret" "stripe_secret_key" {
  name         = "Stripe-SecretKey"
  value        = var.stripe_secret_key
  key_vault_id = azurerm_key_vault.this.id

  depends_on = [azurerm_role_assignment.terraform_secrets_officer]

  lifecycle {
    ignore_changes = [value]
  }
}

resource "azurerm_key_vault_secret" "stripe_webhook_secret" {
  name         = "Stripe-WebhookSecret"
  value        = var.stripe_webhook_secret
  key_vault_id = azurerm_key_vault.this.id

  depends_on = [azurerm_role_assignment.terraform_secrets_officer]

  lifecycle {
    ignore_changes = [value]
  }
}

resource "azurerm_key_vault_secret" "livekit_api_key" {
  name         = "LiveKit-ApiKey"
  value        = var.livekit_api_key
  key_vault_id = azurerm_key_vault.this.id

  depends_on = [azurerm_role_assignment.terraform_secrets_officer]

  lifecycle {
    ignore_changes = [value]
  }
}

resource "azurerm_key_vault_secret" "livekit_api_secret" {
  name         = "LiveKit-ApiSecret"
  value        = var.livekit_api_secret
  key_vault_id = azurerm_key_vault.this.id

  depends_on = [azurerm_role_assignment.terraform_secrets_officer]

  lifecycle {
    ignore_changes = [value]
  }
}

resource "azurerm_key_vault_secret" "report_vendor_api_key" {
  name         = "ReportEvidence-VendorApiKey"
  value        = var.report_vendor_api_key
  key_vault_id = azurerm_key_vault.this.id

  depends_on = [azurerm_role_assignment.terraform_secrets_officer]

  lifecycle {
    ignore_changes = [value]
  }
}
