variable "project_name" {
  type        = string
  description = "Short project slug used in resource names (lowercase alphanumeric)."
  default     = "lfleet"
}

variable "environment" {
  type        = string
  description = "Environment name: staging | production."

  validation {
    condition     = contains(["staging", "production"], var.environment)
    error_message = "environment must be staging or production."
  }
}

variable "location" {
  type        = string
  description = "Azure region."
  default     = "eastus"
}

variable "tags" {
  type    = map(string)
  default = {}
}

variable "app_service_sku" {
  type        = string
  description = "Linux App Service plan SKU."
  default     = "B1"
}

variable "sql_sku_name" {
  type        = string
  description = "Azure SQL database SKU."
  default     = "GP_S_Gen5_1"
}

variable "acr_sku" {
  type    = string
  default = "Basic"
}

variable "docker_image_name" {
  type        = string
  description = "Initial container image name:tag in ACR (CI updates the running tag)."
  default     = "liberationfleet:latest"
}

variable "custom_domain_url" {
  type        = string
  description = "Optional public HTTPS origin override (no trailing slash). Defaults to App Service hostname."
  default     = ""
}

variable "livekit_host" {
  type        = string
  description = "LiveKit WSS URL. Leave empty until LiveKit Cloud / self-host is ready."
  default     = ""
}

variable "sql_firewall_rules" {
  type = list(object({
    name             = string
    start_ip_address = string
    end_ip_address   = string
  }))
  description = "Extra SQL firewall rules (e.g. office IPs for troubleshooting)."
  default     = []
}

variable "key_vault_purge_protection_enabled" {
  type    = bool
  default = false
}

variable "log_retention_days" {
  type    = number
  default = 30
}
