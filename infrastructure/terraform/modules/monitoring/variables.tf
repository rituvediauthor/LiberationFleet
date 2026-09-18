variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "log_analytics_name" {
  type = string
}

variable "app_insights_name" {
  type = string
}

variable "retention_in_days" {
  type    = number
  default = 30
}

variable "daily_quota_gb" {
  type        = number
  description = "Log Analytics daily ingestion cap in GB. Use a small value on staging to bound App Insights cost."
  default     = 1
}

variable "tags" {
  type    = map(string)
  default = {}
}
