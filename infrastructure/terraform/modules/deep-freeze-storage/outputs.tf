output "storage_account_id" {
  value = azurerm_storage_account.deep_freeze.id
}

output "storage_account_name" {
  value = azurerm_storage_account.deep_freeze.name
}

output "container_name" {
  value = azurerm_storage_container.deep_freeze.name
}

output "primary_connection_string" {
  value     = azurerm_storage_account.deep_freeze.primary_connection_string
  sensitive = true
}
