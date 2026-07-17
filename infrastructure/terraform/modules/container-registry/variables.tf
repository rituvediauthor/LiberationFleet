variable "name" {
  type        = string
  description = "ACR name (alphanumeric only)."
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "sku" {
  type        = string
  description = "ACR SKU: Basic, Standard, or Premium."
  default     = "Basic"
}

variable "tags" {
  type    = map(string)
  default = {}
}
