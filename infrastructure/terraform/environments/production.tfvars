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
custom_domain_url = "https://liberationfleet.org"
livekit_host = "wss://lf-prod-p8i37iuy.livekit.cloud"

# Transactional email (Brevo SMTP). User/password live in Key Vault Email-SmtpUser / Email-SmtpPassword.
email_smtp_host    = "smtp-relay.brevo.com"
email_smtp_port    = 587
email_from_address = "noreply@liberationfleet.org"
email_from_name    = "Liberation Fleet"

sql_firewall_rules = [
  {
    name             = "Home"
    start_ip_address = "99.90.217.124"
    end_ip_address   = "99.90.217.124"
  }
]

tags = {
  cost_center = "liberationfleet"
}
