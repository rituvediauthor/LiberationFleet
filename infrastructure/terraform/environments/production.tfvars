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

# Donation acknowledgment letterhead (no home mailing address on receipts)
organization_legal_name      = "Liberation Fleet Co."
organization_ein             = "42-3969519"
organization_mailing_address = ""

# FCM (from google-services.json project_info.project_id)
push_fcm_project_id = "liberationfleet-2bea7"

# APNs. Key ID = 10 characters from Apple → Keys (also the XXXXXXXXXX in AuthKey_XXXXXXXXXX.p8).
# Empty Push__ApnsKeyId means the API never sends to Apple. Set the Key ID here or in App Service, then Restart.
# Team ID is Xcode Signing & Capabilities / developer.apple.com Membership.
push_apns_key_id      = "64MJ3FY5GZ"
push_apns_team_id     = "U4F76ADPUE"
push_apns_bundle_id   = "com.liberationfleet.app"
push_apns_use_sandbox = false

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
