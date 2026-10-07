variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "service_plan_name" {
  type = string
}

variable "app_name" {
  type = string
}

variable "managed_identity_id" {
  type = string
}

variable "managed_identity_client_id" {
  type = string
}

variable "managed_identity_principal_id" {
  type = string
}

variable "sku_name" {
  type        = string
  description = "App Service plan SKU. B1 for staging; P0v3/P1v3 for production."
  default     = "B1"
}

variable "always_on" {
  type        = bool
  description = "Keep the worker process loaded. False on staging so SQL serverless can pause when idle."
  default     = true
}

variable "docker_image_name" {
  type        = string
  description = "Image name including tag, e.g. liberationfleet:latest"
}

variable "acr_login_server" {
  type = string
}

variable "acr_id" {
  type = string
}

variable "aspnetcore_environment" {
  type    = string
  default = "Production"
}

variable "app_public_url" {
  type        = string
  description = "Public HTTPS origin of the app (no trailing slash)."
}

variable "jwt_issuer" {
  type    = string
  default = "LiberationFleet"
}

variable "jwt_audience" {
  type    = string
  default = "LiberationFleetClient"
}

variable "livekit_host" {
  type        = string
  description = "LiveKit WSS URL (LiveKit Cloud or self-hosted)."
  default     = ""
}

variable "livekit_token_ttl_minutes" {
  type    = number
  default = 360
}

variable "application_insights_connection_string" {
  type      = string
  sensitive = true
}

variable "key_vault_secret_uris" {
  type = object({
    jwt_secret_key                = string
    sql_connection_string         = string
    report_evidence_aes_key       = string
    stripe_secret_key             = string
    stripe_webhook_secret         = string
    livekit_api_key               = string
    livekit_api_secret            = string
    report_vendor_api_key         = string
    email_smtp_user               = string
    email_smtp_password           = string
    push_fcm_service_account_json = string
    push_apns_key_p8              = string
  })
}

variable "email_smtp_host" {
  type        = string
  description = "SMTP relay hostname (e.g. smtp-relay.brevo.com)."
  default     = ""
}

variable "email_smtp_port" {
  type    = number
  default = 587
}

variable "email_from_address" {
  type        = string
  description = "Verified From address for transactional mail."
  default     = ""
}

variable "email_from_name" {
  type    = string
  default = "Liberation Fleet"
}

variable "push_fcm_project_id" {
  type        = string
  default     = ""
  description = "Firebase project id for FCM HTTP v1 (non-secret)."
}

variable "push_apns_key_id" {
  type        = string
  default     = ""
  description = "APNs key id from Apple Developer (10 chars)."
}

variable "push_apns_team_id" {
  type        = string
  default     = ""
  description = "Apple Developer Team ID."
}

variable "push_apns_bundle_id" {
  type    = string
  default = "com.liberationfleet.app"
}

variable "push_apns_use_sandbox" {
  type        = bool
  default     = false
  description = "true for TestFlight/debug APNs; false for App Store production."
}

variable "extra_app_settings" {
  type    = map(string)
  default = {}
}

variable "tags" {
  type    = map(string)
  default = {}
}
