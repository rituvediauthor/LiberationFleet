provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy    = false
      recover_soft_deleted_key_vaults = true
    }
    resource_group {
      # Azure auto-creates App Insights "SmartDetection" action groups outside Terraform.
      # Keep false so region rebuilds / destroy can remove the RG without a manual purge.
      prevent_deletion_if_contains_resources = false
    }
  }
}
