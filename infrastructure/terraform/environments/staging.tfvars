project_name = "lfleet"
environment  = "staging"
location     = "eastus"

app_service_sku = "B1"
sql_sku_name    = "GP_S_Gen5_1"
acr_sku         = "Basic"

# Optional: set after provisioning LiveKit Cloud or a self-hosted SFU + TURN.
# livekit_host = "wss://your-project.livekit.cloud"

# Optional: allow your office IP to manage SQL via SSMS / Azure Data Studio.
# sql_firewall_rules = [
#   {
#     name             = "Office"
#     start_ip_address = "1.2.3.4"
#     end_ip_address   = "1.2.3.4"
#   }
# ]

tags = {
  cost_center = "liberationfleet"
}
