variable "name" {
  type        = string
  description = "Storage account name (3-24 lowercase alphanumeric)."
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "container_name" {
  type    = string
  default = "media-deep-freeze"
}

variable "replication_type" {
  type    = string
  default = "LRS"
}

variable "app_principal_id" {
  type        = string
  description = "App managed identity principal for blob access."
}

variable "tags" {
  type    = map(string)
  default = {}
}
