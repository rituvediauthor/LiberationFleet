# One-time bootstrap for Terraform remote state (run locally with Owner/Contributor).
# Usage:
#   cd infrastructure/terraform/bootstrap
#   terraform init
#   terraform apply -var="location=eastus"

terraform {
  required_version = ">= 1.6.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

provider "azurerm" {
  features {}
}

variable "location" {
  type    = string
  default = "eastus"
}

variable "resource_group_name" {
  type    = string
  default = "rg-lfleet-tfstate"
}

resource "random_string" "suffix" {
  length  = 6
  upper   = false
  special = false
}

resource "azurerm_resource_group" "tfstate" {
  name     = var.resource_group_name
  location = var.location
  tags = {
    purpose    = "terraform-state"
    managed_by = "terraform-bootstrap"
  }
}

resource "azurerm_storage_account" "tfstate" {
  name                     = "stlfeet${random_string.suffix.result}"
  resource_group_name      = azurerm_resource_group.tfstate.name
  location                 = azurerm_resource_group.tfstate.location
  account_tier             = "Standard"
  account_replication_type = "GRS"
  min_tls_version          = "TLS1_2"

  blob_properties {
    versioning_enabled = true
  }

  tags = {
    purpose    = "terraform-state"
    managed_by = "terraform-bootstrap"
  }
}

resource "azurerm_storage_container" "tfstate" {
  name                  = "tfstate"
  storage_account_id    = azurerm_storage_account.tfstate.id
  container_access_type = "private"
}

output "resource_group_name" {
  value = azurerm_resource_group.tfstate.name
}

output "storage_account_name" {
  value = azurerm_storage_account.tfstate.name
}

output "container_name" {
  value = azurerm_storage_container.tfstate.name
}

output "backend_hcl_snippet" {
  value = <<-EOT
  resource_group_name  = "${azurerm_resource_group.tfstate.name}"
  storage_account_name = "${azurerm_storage_account.tfstate.name}"
  container_name       = "tfstate"
  key                  = "staging.terraform.tfstate"
  EOT
}
