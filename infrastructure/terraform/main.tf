module "resource_group" {
  source = "./modules/resource-group"

  name     = local.names.resource_group
  location = var.location
  tags     = local.common_tags
}

resource "azurerm_user_assigned_identity" "app" {
  name                = local.names.identity
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.common_tags
}

module "monitoring" {
  source = "./modules/monitoring"

  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  log_analytics_name  = local.names.law
  app_insights_name   = local.names.appi
  retention_in_days   = var.log_retention_days
  tags                = local.common_tags
}

module "container_registry" {
  source = "./modules/container-registry"

  name                = local.names.acr
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku                 = var.acr_sku
  tags                = local.common_tags
}

module "sql" {
  source = "./modules/sql"

  server_name         = local.names.sql_server
  database_name       = local.names.sql_database
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku_name            = var.sql_sku_name
  firewall_rules     = var.sql_firewall_rules
  tags                = local.common_tags
}

module "key_vault" {
  source = "./modules/key-vault"

  name                       = local.names.key_vault
  resource_group_name        = module.resource_group.name
  location                   = module.resource_group.location
  app_principal_id           = azurerm_user_assigned_identity.app.principal_id
  sql_connection_string      = module.sql.ado_net_connection_string
  purge_protection_enabled   = var.key_vault_purge_protection_enabled
  tags                       = local.common_tags
}

module "deep_freeze_storage" {
  source = "./modules/deep-freeze-storage"

  name                = local.names.deep_freeze_storage
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  app_principal_id    = azurerm_user_assigned_identity.app.principal_id
  tags                = local.common_tags
}

resource "azurerm_key_vault_secret" "deep_freeze_connection" {
  name         = "MediaDeepFreeze-AzureConnectionString"
  value        = module.deep_freeze_storage.primary_connection_string
  key_vault_id = module.key_vault.id

  depends_on = [module.key_vault]
}

module "app_service" {
  source = "./modules/app-service"

  resource_group_name                    = module.resource_group.name
  location                               = module.resource_group.location
  service_plan_name                      = local.names.plan
  app_name                               = local.names.app
  managed_identity_id                    = azurerm_user_assigned_identity.app.id
  managed_identity_client_id             = azurerm_user_assigned_identity.app.client_id
  managed_identity_principal_id          = azurerm_user_assigned_identity.app.principal_id
  sku_name                               = var.app_service_sku
  always_on                              = true
  docker_image_name                      = var.docker_image_name
  acr_login_server                       = module.container_registry.login_server
  acr_id                                 = module.container_registry.id
  app_public_url                         = local.app_public_url
  livekit_host                           = var.livekit_host
  application_insights_connection_string = module.monitoring.application_insights_connection_string
  key_vault_secret_uris                  = module.key_vault.secret_uris
  extra_app_settings = {
    "MediaDeepFreeze__Enabled"               = "true"
    "MediaDeepFreeze__AgeDays"               = "60"
    "MediaDeepFreeze__Provider"              = "azure"
    "MediaDeepFreeze__AzureContainerName"    = module.deep_freeze_storage.container_name
    "MediaDeepFreeze__AzureConnectionString" = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.deep_freeze_connection.versionless_id})"
  }
  tags = local.common_tags
}
