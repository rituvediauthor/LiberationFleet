# LiberationFleet Azure hosting

| Path | Purpose |
|------|---------|
| [`infrastructure/terraform`](./terraform) | Azure IaC (App Service, SQL, Key Vault, ACR, App Insights, deep-freeze blob) |
| [`infrastructure/terraform/bootstrap`](./terraform/bootstrap) | One-time remote state storage account |
| [`../azure-pipelines.yml`](../azure-pipelines.yml) | Azure DevOps CI/CD |
| [`../.azure/pipelines`](../.azure/pipelines) | Pipeline templates + setup notes |
| [`livekit.yaml`](./livekit.yaml) | LiveKit server config (self-host / local compose) |
| [`../docs/AZURE-GO-LIVE.md`](../docs/AZURE-GO-LIVE.md) | Go-live steps |
| [`../docs/NATIVE-APPS.md`](../docs/NATIVE-APPS.md) | Capacitor iOS / Android |
| [`../docs/LAUNCH-CHECKLIST.md`](../docs/LAUNCH-CHECKLIST.md) | Stores + third-party services |

Start with [`terraform/README.md`](./terraform/README.md).
