project_name = "lfleet"
environment  = "production"
location     = "eastus"

app_service_sku = "P0v3"
sql_sku_name    = "GP_S_Gen5_2"
acr_sku         = "Standard"

key_vault_purge_protection_enabled = true
log_retention_days                 = 90

# Prefer a custom domain once DNS is ready:
# custom_domain_url = "https://app.example.com"
# livekit_host      = "wss://your-project.livekit.cloud"

tags = {
  cost_center = "liberationfleet"
}
