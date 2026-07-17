# Remote state — configure via `-backend-config=` in CI or a local `backend.hcl`.
# See environments/*.backend.hcl.example and docs in README.md.
terraform {
  backend "azurerm" {}
}
