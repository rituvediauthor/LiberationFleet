output "id" {
  value = azurerm_key_vault.this.id
}

output "name" {
  value = azurerm_key_vault.this.name
}

output "uri" {
  value = azurerm_key_vault.this.vault_uri
}

output "secret_uris" {
  description = "Key Vault secret URIs for App Service key vault references."
  value = {
    jwt_secret_key              = azurerm_key_vault_secret.jwt_secret.versionless_id
    sql_connection_string       = azurerm_key_vault_secret.sql_connection_string.versionless_id
    report_evidence_aes_key     = azurerm_key_vault_secret.report_evidence_aes_key.versionless_id
    stripe_secret_key           = azurerm_key_vault_secret.stripe_secret_key.versionless_id
    stripe_webhook_secret       = azurerm_key_vault_secret.stripe_webhook_secret.versionless_id
    livekit_api_key             = azurerm_key_vault_secret.livekit_api_key.versionless_id
    livekit_api_secret          = azurerm_key_vault_secret.livekit_api_secret.versionless_id
    report_vendor_api_key       = azurerm_key_vault_secret.report_vendor_api_key.versionless_id
  }
}
