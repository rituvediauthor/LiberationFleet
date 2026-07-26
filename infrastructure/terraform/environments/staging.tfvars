project_name = "lfleet"
environment  = "staging"
location     = "westus"

app_service_sku = "B1"
sql_sku_name    = "GP_S_Gen5_1"
acr_sku         = "Basic"

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
