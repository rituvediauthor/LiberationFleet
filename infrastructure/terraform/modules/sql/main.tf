resource "random_password" "sql_admin" {
  length           = 32
  special          = true
  override_special = "!#$%&*()-_=+[]{}<>:?"
}

resource "azurerm_mssql_server" "this" {
  name                         = var.server_name
  resource_group_name          = var.resource_group_name
  location                     = var.location
  version                      = "12.0"
  administrator_login          = var.administrator_login
  administrator_login_password = random_password.sql_admin.result
  minimum_tls_version          = "1.2"
  tags                         = var.tags
}

resource "azurerm_mssql_database" "this" {
  name                        = var.database_name
  server_id                   = azurerm_mssql_server.this.id
  collation                   = "SQL_Latin1_General_CP1_CI_AS"
  max_size_gb                 = var.max_size_gb
  sku_name                    = var.sku_name
  auto_pause_delay_in_minutes = var.sku_name == "GP_S_Gen5_1" || startswith(var.sku_name, "GP_S_") ? var.auto_pause_delay_in_minutes : null
  min_capacity                = startswith(var.sku_name, "GP_S_") ? var.min_capacity : null
  zone_redundant              = false
  tags                        = var.tags

  short_term_retention_policy {
    retention_days = var.short_term_retention_days
  }
}

# Allow Azure services (App Service outbound) to reach SQL.
resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.this.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_mssql_firewall_rule" "extra" {
  for_each = { for rule in var.firewall_rules : rule.name => rule }

  name             = each.value.name
  server_id        = azurerm_mssql_server.this.id
  start_ip_address = each.value.start_ip_address
  end_ip_address   = each.value.end_ip_address
}
