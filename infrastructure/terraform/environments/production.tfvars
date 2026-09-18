project_name = "lfleet"
environment  = "production"
location     = "westus"

# Stay on the cheap DTU / Basic-plan stack until traffic justifies a bump.
# P0v3 + serverless SQL alone can exceed a $60/mo wallet before users arrive.
app_service_sku = "B1"
sql_sku_name    = "S0"
sql_max_size_gb = 10
acr_sku         = "Basic"

key_vault_purge_protection_enabled = true
log_retention_days                 = 30

# Prefer a custom domain once DNS is ready:
# custom_domain_url = "https://app.example.com"
# livekit_host      = "wss://your-project.livekit.cloud"

tags = {
  cost_center = "liberationfleet"
}
