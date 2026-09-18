project_name = "lfleet"
environment  = "staging"
location     = "westus"

# Budget stack (~$40–55/mo ballpark): B1 app + DTU SQL (not serverless).
# Serverless GP_S looks cheap when paused, but awake time at 0.5 vCore still
# forecasts like $200+/mo SQL alone — same trap as the $310 burn.
app_service_sku = "B1"
sql_sku_name    = "S0"
sql_max_size_gb = 5
acr_sku         = "Basic"
log_retention_days = 7

# LiveKit Cloud WSS URL (Path B). Keys go in Key Vault, not here.
livekit_host = "wss://liberation-fleet-lsb02tua.livekit.cloud"

# Optional: allow your office IP to manage SQL via SSMS / Azure Data Studio.
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
