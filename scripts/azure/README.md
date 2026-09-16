# EventPulse Azure Development Environment Provisioning

This directory contains the automated, idempotent Azure CLI provisioning script for the EventPulse development infrastructure.

---

## 1. Prerequisites

Before running the provisioning script, ensure the following prerequisites are met on your local terminal or workstation:

1. **Azure CLI**:
   - Verify Azure CLI is installed:
     ```bash
     az --version
     ```
   - If not installed, download from [Azure CLI Installation Guide](https://aka.ms/installazurecliwindows).

2. **Azure Authentication**:
   - Log in to your Azure account:
     ```bash
     az login
     ```

3. **Active Subscription**:
   - Ensure the correct Azure subscription containing your existing resources (`rg-eventpulse-dev`) is selected:
     ```bash
     # List subscriptions
     az account list --output table

     # Set active subscription
     az account set --subscription "<your-subscription-id-or-name>"

     # Verify
     az account show --output table
     ```

---

## 2. How to Run

Execute the script using PowerShell from the root of the repository:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\azure\provision-dev.ps1
```

### Optional Custom Parameters

You can customize parameters if needed:

```powershell
# Specify custom storage account name and enable public blob access
powershell -ExecutionPolicy Bypass -File .\scripts\azure\provision-dev.ps1 `
    -ResourceGroupName "rg-eventpulse-dev" `
    -Location "southeastasia" `
    -AppServicePlanName "asp-eventpulse-dev" `
    -StorageAccountName "steventpulsedev01" `
    -ContainerName "event-posters" `
    -EnablePublicBlobAccess
```

---

## 3. What It Creates

The script checks each resource before creation. If a resource already exists, it is safely reused without failing. Only missing resources are provisioned:

1. **Linux Web Apps** (on existing Linux App Service Plan `asp-eventpulse-dev`):
   - `eventpulse-event-dev` (.NET 10 on Linux)
   - `eventpulse-booking-dev` (.NET 10 on Linux)
   - `eventpulse-payment-dev` (.NET 10 on Linux)
   - `eventpulse-gateway-dev` (.NET 10 on Linux)
2. **Platform Security Baselines**:
   - HTTPS-only traffic enforced on all web apps (`--https-only true`).
   - Minimum TLS version set to 1.2 (`--min-tls-version 1.2`).
   - FTPS disabled for secure attack surface reduction (`--ftps-state Disabled`).
   - Explicit runtime framework setting (`DOTNETCORE|10.0`).
3. **Azure Blob Storage**:
   - Standard LRS Storage Account (`steventpulsedev` or unique deterministic name).
   - Blob Container: `event-posters`.
   - By default, creates private container access (or blob-level access if `-EnablePublicBlobAccess` is supplied).

---

## 4. What It Intentionally Does NOT Create

To protect your existing environment and control costs, this script intentionally:

- **Does NOT create a new Resource Group** (reuses `rg-eventpulse-dev`).
- **Does NOT create a new App Service Plan** (reuses `asp-eventpulse-dev`).
- **Does NOT recreate or modify `eventpulse-identity-dev`** (Identity Service is already deployed and operational; its configuration is left untouched).
- **Does NOT create new databases or servers** (reuses existing `pg-eventpulse-dev` and existing logical databases: `eventpulse_identity`, `eventpulse_events`, `eventpulse_booking`, `eventpulse_payments`).
- **Does NOT create Virtual Networks, Private Endpoints, or NAT Gateways** (Web Apps remain publicly accessible with TLS for university dev environment).
- **Does NOT hardcode or inject secret credentials into git or code**.

---

## 5. Secrets and Configuration to Configure Separately

Per cloud security best practices, secrets and database credentials must never be committed to source code or embedded in provisioning scripts. Configure them in Azure App Service Configuration (Portal or CLI):

### A. Event Service (`eventpulse-event-dev`)

| Setting Name | Description / Example Value |
| :--- | :--- |
| `ConnectionStrings__EventDatabase` | `Host=pg-eventpulse-dev.postgres.database.azure.com;Port=5432;Database=eventpulse_events;Username=<admin>;Password=<secret>;Ssl Mode=Require;` |
| `Jwt__Issuer` | `EventPulse.IdentityService` |
| `Jwt__Audience` | `EventPulse.Clients` |
| `Jwt__Key` | `<Same 256-bit signing key configured in eventpulse-identity-dev>` |
| `BlobStorage__ConnectionString` | Connection string retrieved from Azure Storage Account: <br>`az storage account show-connection-string --name <storage-account> -g rg-eventpulse-dev --query connectionString -o tsv` |
| `BlobStorage__ContainerName` | `event-posters` |

### B. Booking Service (`eventpulse-booking-dev`)

> **Architecture Note**: The Booking Service currently contains the routing skeleton, controllers, and health check endpoint (`/health`). It does not currently register an EF Core DbContext or database connection string in `Program.cs`.

| Setting Name | Value |
| :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | `Development` (or `Production`) |

*(No database connection string required until Booking Service adds EF Core persistence).*

### C. Payment Service (`eventpulse-payment-dev`)

> **Architecture Note**: The Payment Service currently contains the routing skeleton, controllers, and health check endpoint (`/health`). It does not currently register an EF Core DbContext or database connection string in `Program.cs`.

| Setting Name | Value |
| :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | `Development` (or `Production`) |

*(No database connection string required until Payment Service adds EF Core persistence).*

### D. Gateway (`eventpulse-gateway-dev` - YARP Reverse Proxy)

The Gateway uses YARP to route traffic from clients to downstream backend services. In Azure, set downstream cluster destination addresses:

| Setting Name | Value |
| :--- | :--- |
| `ReverseProxy__Clusters__identity_cluster__Destinations__identity_service__Address` | `https://eventpulse-identity-dev.azurewebsites.net` |
| `ReverseProxy__Clusters__event_cluster__Destinations__event_service__Address` | `https://eventpulse-event-dev.azurewebsites.net` |
| `ReverseProxy__Clusters__booking_cluster__Destinations__booking_service__Address` | `https://eventpulse-booking-dev.azurewebsites.net` |
| `ReverseProxy__Clusters__payment_cluster__Destinations__payment_service__Address` | `https://eventpulse-payment-dev.azurewebsites.net` |

---

## 6. How to Verify Created Apps

Once provisioned and deployed, verify that the health check endpoints are responding with HTTP 200:

```bash
# 1. Identity Service (Existing)
curl -i https://eventpulse-identity-dev.azurewebsites.net/health

# 2. Event Service
curl -i https://eventpulse-event-dev.azurewebsites.net/health

# 3. Booking Service
curl -i https://eventpulse-booking-dev.azurewebsites.net/health

# 4. Payment Service
curl -i https://eventpulse-payment-dev.azurewebsites.net/health

# 5. Gateway
curl -i https://eventpulse-gateway-dev.azurewebsites.net/health
```

You can also test routing through the Gateway:
```bash
# Verify Gateway proxies to Identity health check or auth endpoint
curl -i https://eventpulse-gateway-dev.azurewebsites.net/health
curl -i https://eventpulse-gateway-dev.azurewebsites.net/api/events
```
