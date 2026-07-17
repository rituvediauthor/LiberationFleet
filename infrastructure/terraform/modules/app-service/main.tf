resource "azurerm_service_plan" "this" {
  name                = var.service_plan_name
  resource_group_name = var.resource_group_name
  location            = var.location
  os_type             = "Linux"
  sku_name            = var.sku_name
  tags                = var.tags
}

resource "azurerm_linux_web_app" "this" {
  name                = var.app_name
  resource_group_name = var.resource_group_name
  location            = var.location
  service_plan_id     = azurerm_service_plan.this.id
  https_only          = true
  tags                = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [var.managed_identity_id]
  }

  key_vault_reference_identity_id = var.managed_identity_id

  site_config {
    always_on                                         = var.always_on
    ftps_state                                        = "Disabled"
    minimum_tls_version                               = "1.2"
    container_registry_use_managed_identity           = true
    container_registry_managed_identity_client_id     = var.managed_identity_client_id
    health_check_path                                 = "/"
    health_check_eviction_time_in_min                 = 5
    websockets_enabled                                = true
    http2_enabled                                     = true

    application_stack {
      docker_image_name   = var.docker_image_name
      docker_registry_url = "https://${var.acr_login_server}"
    }
  }

  app_settings = merge(
    {
      WEBSITES_ENABLE_APP_SERVICE_STORAGE        = "false"
      WEBSITES_PORT                              = "8080"
      DOCKER_ENABLE_CI                           = "false"
      ASPNETCORE_ENVIRONMENT                     = var.aspnetcore_environment
      ASPNETCORE_URLS                            = "http://+:8080"
      "Jwt__Issuer"                              = var.jwt_issuer
      "Jwt__Audience"                            = var.jwt_audience
      "Cors__AllowedOrigins__0"                  = var.app_public_url
      "Cors__AllowedOrigins__1"                  = "capacitor://localhost"
      "Cors__AllowedOrigins__2"                  = "ionic://localhost"
      "Cors__AllowedOrigins__3"                  = "https://localhost"
      "Cors__AllowedOrigins__4"                  = "http://localhost"
      "LiveKit__Host"                            = var.livekit_host
      "LiveKit__TokenTtlMinutes"                 = tostring(var.livekit_token_ttl_minutes)
      "Stripe__PublicAppBaseUrl"                 = var.app_public_url
      "APPLICATIONINSIGHTS_CONNECTION_STRING"    = var.application_insights_connection_string
      ApplicationInsightsAgent_EXTENSION_VERSION = "~3"

      "ConnectionStrings__DefaultConnection" = "@Microsoft.KeyVault(SecretUri=${var.key_vault_secret_uris.sql_connection_string})"
      "Jwt__SecretKey"                       = "@Microsoft.KeyVault(SecretUri=${var.key_vault_secret_uris.jwt_secret_key})"
      "ReportEvidence__AesKeyBase64"         = "@Microsoft.KeyVault(SecretUri=${var.key_vault_secret_uris.report_evidence_aes_key})"
      "Stripe__SecretKey"                    = "@Microsoft.KeyVault(SecretUri=${var.key_vault_secret_uris.stripe_secret_key})"
      "Stripe__WebhookSecret"                = "@Microsoft.KeyVault(SecretUri=${var.key_vault_secret_uris.stripe_webhook_secret})"
      "LiveKit__ApiKey"                      = "@Microsoft.KeyVault(SecretUri=${var.key_vault_secret_uris.livekit_api_key})"
      "LiveKit__ApiSecret"                   = "@Microsoft.KeyVault(SecretUri=${var.key_vault_secret_uris.livekit_api_secret})"
      "ReportEvidence__VendorApiKey"         = "@Microsoft.KeyVault(SecretUri=${var.key_vault_secret_uris.report_vendor_api_key})"
    },
    var.extra_app_settings
  )

  logs {
    detailed_error_messages = true
    failed_request_tracing  = true

    http_logs {
      file_system {
        retention_in_days = 7
        retention_in_mb   = 35
      }
    }
  }

  lifecycle {
    ignore_changes = [
      site_config[0].application_stack[0].docker_image_name,
    ]
  }
}

resource "azurerm_role_assignment" "acr_pull" {
  scope                = var.acr_id
  role_definition_name = "AcrPull"
  principal_id         = var.managed_identity_principal_id
}
