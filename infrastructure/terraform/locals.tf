locals {
  name_prefix = "${var.project_name}-${var.environment}"

  # Azure naming constraints applied per resource type.
  names = {
    resource_group = "rg-${local.name_prefix}"
    # ACR: 5-50 alphanumeric only
    acr = substr(replace("${var.project_name}${var.environment}acr", "-", ""), 0, 50)
    # Key Vault: 3-24 alphanumeric
    key_vault = substr(replace("${var.project_name}${var.environment}kv", "-", ""), 0, 24)
    # SQL server: lowercase letters, numbers, hyphens
    sql_server   = "sql-${local.name_prefix}"
    sql_database = "LiberationFleetDb"
    plan         = "asp-${local.name_prefix}"
    app          = "app-${local.name_prefix}"
    identity     = "id-${local.name_prefix}-app"
    law          = "law-${local.name_prefix}"
    appi         = "appi-${local.name_prefix}"
    # Storage account: 3-24 lowercase alphanumeric
    deep_freeze_storage = substr(replace("${var.project_name}${var.environment}df", "-", ""), 0, 24)
  }

  default_app_url = "https://${local.names.app}.azurewebsites.net"
  app_public_url  = var.custom_domain_url != "" ? var.custom_domain_url : local.default_app_url

  common_tags = merge(
    {
      project     = var.project_name
      environment = var.environment
      managed_by  = "terraform"
    },
    var.tags
  )
}
