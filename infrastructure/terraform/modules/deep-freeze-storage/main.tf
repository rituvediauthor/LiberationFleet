resource "azurerm_storage_account" "deep_freeze" {
  name                     = var.name
  resource_group_name      = var.resource_group_name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = var.replication_type
  min_tls_version          = "TLS1_2"
  allow_nested_items_to_be_public = false
  tags                     = var.tags

  blob_properties {
    versioning_enabled = false
    delete_retention_policy {
      days = 7
    }
  }
}

resource "azurerm_storage_container" "deep_freeze" {
  name                  = var.container_name
  storage_account_id    = azurerm_storage_account.deep_freeze.id
  container_access_type = "private"
}

resource "azurerm_role_assignment" "app_blob_contributor" {
  scope                = azurerm_storage_account.deep_freeze.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = var.app_principal_id
}
