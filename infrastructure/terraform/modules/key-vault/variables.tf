variable "name" {
  type        = string
  description = "Key Vault name (globally unique, 3-24 alphanumeric)."
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "app_principal_id" {
  type        = string
  description = "Principal ID of the App Service managed identity."
}

variable "sql_connection_string" {
  type      = string
  sensitive = true
}

variable "purge_protection_enabled" {
  type    = bool
  default = false
}

variable "stripe_secret_key" {
  type      = string
  sensitive = true
  default   = "change-me-stripe-secret-key"
}

variable "stripe_webhook_secret" {
  type      = string
  sensitive = true
  default   = "change-me-stripe-webhook-secret"
}

variable "livekit_api_key" {
  type      = string
  sensitive = true
  default   = "change-me"
}

variable "livekit_api_secret" {
  type      = string
  sensitive = true
  default   = "change-me-livekit-api-secret-min-32-chars"
}

variable "report_vendor_api_key" {
  type      = string
  sensitive = true
  default   = "change-me-report-vendor-key"
}

variable "tags" {
  type    = map(string)
  default = {}
}
