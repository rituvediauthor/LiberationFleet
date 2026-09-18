variable "server_name" {
  type        = string
  description = "Azure SQL logical server name (globally unique)."
}

variable "database_name" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "administrator_login" {
  type    = string
  default = "lfadmin"
}

variable "sku_name" {
  type        = string
  description = "Database SKU. S0 (~flat monthly) for budget; GP_S_* only when idle pause dominates the month."
  default     = "S0"
}

variable "max_size_gb" {
  type        = number
  description = "Max data size GB. Must match SKU limits (Basic=2, S0<=250)."
  default     = 5
}

variable "min_capacity" {
  type    = number
  default = 0.5
}

variable "auto_pause_delay_in_minutes" {
  type        = number
  description = "Serverless idle minutes before pause. Azure minimum is 15."
  default     = 15
}

variable "short_term_retention_days" {
  type    = number
  default = 7
}

variable "firewall_rules" {
  type = list(object({
    name             = string
    start_ip_address = string
    end_ip_address   = string
  }))
  default = []
}

variable "tags" {
  type    = map(string)
  default = {}
}
