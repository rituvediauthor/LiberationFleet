output "server_id" {
  value = azurerm_mssql_server.this.id
}

output "server_fqdn" {
  value = azurerm_mssql_server.this.fully_qualified_domain_name
}

output "database_id" {
  value = azurerm_mssql_database.this.id
}

output "database_name" {
  value = azurerm_mssql_database.this.name
}

output "administrator_login" {
  value = azurerm_mssql_server.this.administrator_login
}

output "administrator_password" {
  value     = random_password.sql_admin.result
  sensitive = true
}

output "ado_net_connection_string" {
  description = "SQL connection string for the app (Encrypt=True for Azure SQL)."
  value = format(
    "Server=tcp:%s,1433;Initial Catalog=%s;Persist Security Info=False;User ID=%s;Password=%s;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    azurerm_mssql_server.this.fully_qualified_domain_name,
    azurerm_mssql_database.this.name,
    azurerm_mssql_server.this.administrator_login,
    random_password.sql_admin.result
  )
  sensitive = true
}
