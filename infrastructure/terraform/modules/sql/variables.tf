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
  description = "Database SKU. GP_S_Gen5_1 is serverless Gen5 1 vCore (cost-conscious)."
  default     = "GP_S_Gen5_1"
}

variable "max_size_gb" {
  type    = number
  default = 32
}

variable "min_capacity" {
  type    = number
  default = 0.5
}

variable "auto_pause_delay_in_minutes" {
  type    = number
  default = 60
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
