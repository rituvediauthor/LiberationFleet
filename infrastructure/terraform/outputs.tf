output "resource_group_name" {
  value = module.resource_group.name
}

output "location" {
  value = module.resource_group.location
}

output "acr_name" {
  value = module.container_registry.name
}

output "acr_login_server" {
  value = module.container_registry.login_server
}

output "web_app_name" {
  value = module.app_service.web_app_name
}

output "web_app_default_hostname" {
  value = module.app_service.default_hostname
}

output "app_public_url" {
  value = local.app_public_url
}

output "key_vault_name" {
  value = module.key_vault.name
}

output "sql_server_fqdn" {
  value = module.sql.server_fqdn
}

output "sql_database_name" {
  value = module.sql.database_name
}

output "application_insights_name" {
  value = local.names.appi
}

output "managed_identity_client_id" {
  value = azurerm_user_assigned_identity.app.client_id
}
