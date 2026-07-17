output "service_plan_id" {
  value = azurerm_service_plan.this.id
}

output "web_app_id" {
  value = azurerm_linux_web_app.this.id
}

output "web_app_name" {
  value = azurerm_linux_web_app.this.name
}

output "default_hostname" {
  value = azurerm_linux_web_app.this.default_hostname
}

output "outbound_ip_addresses" {
  value = azurerm_linux_web_app.this.outbound_ip_address_list
}
