# EventPulse Azure Deployment & Runtime Configuration Guide

This document defines the complete runtime environment variables, application settings, connection strings, and continuous deployment (CD) authentication requirements for running EventPulse microservices on **Azure App Service (Linux)**.

---

## 1. Architecture & Deployment Topology

EventPulse runs on Azure using an App Service Plan (Linux) hosting 5 independent containerized or code-deployed microservices, backed by Azure Database for PostgreSQL Flexible Server and Azure Blob Storage:

```
                          [ Internet / User Browser ]
                                      │
                                      ▼
                        ┌───────────────────────────┐
                        │   eventpulse-gateway-dev  │
                        │      (YARP Gateway)       │
                        └─────────────┬─────────────┘
          ┌──────────────────┬────────┴─────────┬──────────────────┐
          │                  │                  │                  │
          ▼                  ▼                  ▼                  ▼
┌───────────────────┐ ┌──────────────┐ ┌────────────────┐ ┌────────────────┐
│eventpulse-identity│ │eventpulse-   │ │eventpulse-     │ │eventpulse-     │
│       -dev        │ │  event-dev   │ │  booking-dev   │ │  payment-dev   │
└─────────┬─────────┘ └──────┬───────┘ └────────────────┘ └────────────────┘
          │                  │                  
          │  ┌───────────────┴───────────────┐
          │  │ Azure PostgreSQL Flexible     │
          │  │ Server (pg-eventpulse-dev)    │
          │  │  - eventpulse_identity        │
          │  │  - eventpulse_events          │
          │  └───────────────────────────────┘
          │
          │  ┌───────────────────────────────┐
          └──┤ Azure Blob Storage            │
             │ Container: event-posters      │
             └───────────────────────────────┘
```

### Existing Azure Resources (Dev Environment)

| Resource | Resource Name | Purpose |
| :--- | :--- | :--- |
| **Resource Group** | `rg-eventpulse-dev` | Resource container (Region: `Southeast Asia`) |
| **App Service Plan** | `asp-eventpulse-dev` | Linux App Service Plan (B1 / P1v3) |
| **Gateway App Service** | `eventpulse-gateway-dev` | YARP Reverse Proxy & Single Public Entrypoint |
| **Identity App Service** | `eventpulse-identity-dev` | Authentication, RBAC, Tokens, Organizer Approval |
| **Event App Service** | `eventpulse-event-dev` | Events Catalog, Submissions, Admin Reviews, Images |
| **Booking App Service** | `eventpulse-booking-dev` | Ticket Reservations & Seat Inventory (Future) |
| **Payment App Service** | `eventpulse-payment-dev` | Payment Processing & Webhooks (Future) |
| **PostgreSQL Flexible Server** | `pg-eventpulse-dev` | Managed PostgreSQL Server (Port 5432) |
| **Storage Account** | `saeventpulsedev` | Azure Blob Storage for uploaded event banners/posters |

---

## 2. GitHub Actions CI/CD: Azure OIDC Federation & Backend CD Pipeline

### 2.1. Backend CD Lifecycle

The continuous integration and delivery lifecycle operates as follows:

```
Feature Branch
      │
      ▼
Pull Request (PR)
      │
      ▼
EventPulse CI (.github/workflows/ci.yml)
(Build + Unit Tests across backend & frontend)
      │
      ▼
Merge to `main`
      │
      ▼
EventPulse CD (.github/workflows/cd.yml)
      │
      ├─► 1. Validate: Full restore, Release build & test run
      │
      ├─► 2. Deploy Core Services (Parallel Matrix):
      │       ├── Identity Service  ──► Deploy ──► Health Check (/health)
      │       ├── Event Service     ──► Deploy ──► Health Check (/health)
      │       ├── Booking Service   ──► Deploy ──► Health Check (/health)
      │       └── Payment Service   ──► Deploy ──► Health Check (/health)
      │
      └─► 3. Deploy Gateway:
              └── YARP Gateway      ──► Deploy ──► Health Check (/health)
```

### 2.2. Architectural Principles

- **Azure OIDC Federation**: Authenticates via Azure OpenID Connect Workload Identity Federation using `azure/login@v2` (`permissions: id-token: write`). No client secret keys or persistent credentials are stored in GitHub.
- **No Docker**: Microservices are published directly via `dotnet publish -c Release` and deployed natively to Linux App Services via `azure/webapps-deploy@v3`.
- **No Publish Profiles / No `AZURE_CREDENTIALS`**: Zero XML profiles or legacy JSON credentials.
- **Runtime Secrets Stay in Azure**: Database connection strings, JWT signing keys, Brevo SMTP keys, and Blob storage connection strings are managed entirely within Azure App Service Application Settings. The CD pipeline does not expose or handle runtime secrets.
- **Main Is the Deployment Branch**: Azure Entra ID federated credentials map to `repo:<org>/EventPulse:ref:refs/heads/main`. CD executes only on push to `main` or manual `workflow_dispatch`.

### 2.3. GitHub Repository Variables (`vars.*`)

The CD workflow uses GitHub repository configuration variables (accessed via `vars.*`):

| Variable Name | Description | Example / Target |
| :--- | :--- | :--- |
| `AZURE_CLIENT_ID` | Application (Client) ID of the Azure AD App registration | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `AZURE_TENANT_ID` | Directory (Tenant) ID of Microsoft Entra ID | `yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy` |
| `AZURE_SUBSCRIPTION_ID` | Subscription ID hosting `rg-eventpulse-dev` | `zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz` |
| `AZURE_RG_NAME` | Resource group name | `rg-eventpulse-dev` |
| `AZURE_IDENTITY_APP_NAME` | App Service name for Identity | `eventpulse-identity-dev` |
| `AZURE_EVENT_APP_NAME` | App Service name for Event Catalog | `eventpulse-event-dev` |
| `AZURE_BOOKING_APP_NAME` | App Service name for Booking | `eventpulse-booking-dev` |
| `AZURE_PAYMENT_APP_NAME` | App Service name for Payment | `eventpulse-payment-dev` |
| `AZURE_GATEWAY_APP_NAME` | App Service name for YARP Gateway | `eventpulse-gateway-dev` |

---

## 3. Configuration Convention for ASP.NET Core on Linux

In ASP.NET Core on Linux App Service:
- Standard colon `:` delimiters in JSON hierarchical configuration are represented as **double underscores `__`** in environment variables and App Service Application Settings.
- Connection strings can be placed in Azure App Service under the **Connection strings** blade (type `PostgreSQL`) or under **Application settings** using the standard prefix `ConnectionStrings__<Name>`.

---

## 4. Service Configuration Specifications

### 4.1. Identity Service (`eventpulse-identity-dev`)

Identity Service manages authentication, JWT issuance, user profiles, and organizer verification.

#### Connection Strings

| Name | Azure Env Var / App Setting | Type | Example / Format |
| :--- | :--- | :--- | :--- |
| `IdentityDatabase` | `ConnectionStrings__IdentityDatabase` | PostgreSQL | `Host=pg-eventpulse-dev.postgres.database.azure.com;Port=5432;Database=eventpulse_identity;Username=eventpulseadmin;Password=<DB_PASSWORD>;Ssl Mode=Require;Trust Server Certificate=true;` |

#### Application Settings

| Setting Name | Azure Key (`__`) | Required? | Sensitive? | Default / Recommended Azure Value |
| :--- | :--- | :---: | :---: | :--- |
| `Jwt:Key` | `Jwt__Key` | **Yes** | **Yes** | 256-bit+ secure random secret key (minimum 32 chars). **MUST MATCH `Jwt__Key` in Event Service**. |
| `Jwt:Issuer` | `Jwt__Issuer` | **Yes** | No | `EventPulse.IdentityService` |
| `Jwt:Audience` | `Jwt__Audience` | **Yes** | No | `EventPulse.Clients` |
| `Jwt:ExpiryMinutes` | `Jwt__ExpiryMinutes` | No | No | `60` |
| `AdminBootstrap:Enabled` | `AdminBootstrap__Enabled` | Optional | No | `true` during first startup to seed admin, then `false`. |
| `AdminBootstrap:Email` | `AdminBootstrap__Email` | Optional | No | `admin@eventpulse.com` |
| `AdminBootstrap:Password` | `AdminBootstrap__Password` | Optional | **Yes** | Strong temporary password for bootstrap admin. |
| `AdminBootstrap:FullName` | `AdminBootstrap__FullName` | Optional | No | `System Administrator` |
| `DevOrganizer:Enabled` | `DevOrganizer__Enabled` | No | No | `false` in production/Azure. |
| `Email:Provider` | `Email__Provider` | No | No | `Brevo` or `Smtp` (or `Console` for staging/dev). |
| `Email:ApiKey` | `Email__ApiKey` | Conditional | **Yes** | Brevo API key (`xkeysib-...`) if using Brevo provider. |
| `Email:FromEmail` | `Email__FromEmail` | Conditional | No | Verified sender address (e.g. `noreply@eventpulse.com`). |
| `Email:FromName` | `Email__FromName` | No | No | `EventPulse` |
| `Authentication:Google:ClientId` | `Authentication__Google__ClientId` | Optional | No | Google OAuth 2.0 Web Client ID. |
| `Authentication:Google:ClientSecret`| `Authentication__Google__ClientSecret`| Optional | **Yes** | Google OAuth 2.0 Client Secret. |
| `Cors:AllowedOrigins:0` | `Cors__AllowedOrigins__0` | No | No | `https://eventpulse-gateway-dev.azurewebsites.net` or frontend URL. |

---

### 4.2. Event Service (`eventpulse-event-dev`)

Event Service manages event records, categorization, venue types, organizer submissions, admin approvals, and poster uploads.

#### Connection Strings

| Name | Azure Env Var / App Setting | Type | Example / Format |
| :--- | :--- | :--- | :--- |
| `EventDatabase` | `ConnectionStrings__EventDatabase` | PostgreSQL | `Host=pg-eventpulse-dev.postgres.database.azure.com;Port=5432;Database=eventpulse_events;Username=eventpulseadmin;Password=<DB_PASSWORD>;Ssl Mode=Require;Trust Server Certificate=true;` |

#### Application Settings

| Setting Name | Azure Key (`__`) | Required? | Sensitive? | Default / Recommended Azure Value |
| :--- | :--- | :---: | :---: | :--- |
| `Database:MigrateOnStartup` | `Database__MigrateOnStartup` | **Yes** | No | `true` in Azure to automatically apply EF Core migrations on deployment startup. |
| `Database:SeedOnStartup` | `Database__SeedOnStartup` | No | No | `false` (default) to ensure test/demo events are never seeded in production. |
| `Jwt:Key` | `Jwt__Key` | **Yes** | **Yes** | **MUST EXACTLY MATCH `Jwt__Key` configured in Identity Service** to validate incoming JWTs. |
| `Jwt:Issuer` | `Jwt__Issuer` | **Yes** | No | `EventPulse.IdentityService` |
| `Jwt:Audience` | `Jwt__Audience` | **Yes** | No | `EventPulse.Clients` |
| `BlobStorage:ConnectionString` | `BlobStorage__ConnectionString` | **Yes** | **Yes** | Azure Storage Account connection string: `DefaultEndpointsProtocol=https;AccountName=saeventpulsedev;AccountKey=<Key>;EndpointSuffix=core.windows.net` |
| `BlobStorage:ContainerName` | `BlobStorage__ContainerName` | **Yes** | No | `event-posters` |

---

### 4.3. Booking Service (`eventpulse-booking-dev`)

Booking Service manages ticket reservation and checkout session state.

#### Baseline Application Settings

| Setting Name | Azure Key (`__`) | Required? | Sensitive? | Default / Recommended Azure Value |
| :--- | :--- | :---: | :---: | :--- |
| `Logging:LogLevel:Default` | `Logging__LogLevel__Default` | No | No | `Information` |

*(Note: Database connection and Kafka message broker settings will be configured when asynchronous booking choreography is implemented.)*

---

### 4.4. Payment Service (`eventpulse-payment-dev`)

Payment Service manages payment verification, provider integrations, and webhook processing.

#### Baseline Application Settings

| Setting Name | Azure Key (`__`) | Required? | Sensitive? | Default / Recommended Azure Value |
| :--- | :--- | :---: | :---: | :--- |
| `Logging:LogLevel:Default` | `Logging__LogLevel__Default` | No | No | `Information` |

*(Note: Stripe / Payment Gateway API keys and webhook secrets will be configured when payment processing is implemented.)*

---

### 4.5. Gateway Service (`eventpulse-gateway-dev`)

The Gateway is the unified entrypoint for the EventPulse backend. It runs YARP (Yet Another Reverse Proxy) and routes client requests to the internal microservices.

> [!IMPORTANT]
> The source file `backend/gateway/src/EventPulse.Gateway/appsettings.json` retains `http://localhost:710X` for local development.
> In Azure, YARP destination addresses are overridden via **Azure App Service Application Settings**.

#### Application Settings (YARP Destination Overrides)

| Cluster Name | Azure Key (`__`) | Target Value |
| :--- | :--- | :--- |
| `identity_cluster` | `ReverseProxy__Clusters__identity_cluster__Destinations__identity_service__Address` | `https://eventpulse-identity-dev.azurewebsites.net` |
| `event_cluster` | `ReverseProxy__Clusters__event_cluster__Destinations__event_service__Address` | `https://eventpulse-event-dev.azurewebsites.net` |
| `booking_cluster` | `ReverseProxy__Clusters__booking_cluster__Destinations__booking_service__Address` | `https://eventpulse-booking-dev.azurewebsites.net` |
| `payment_cluster` | `ReverseProxy__Clusters__payment_cluster__Destinations__payment_service__Address` | `https://eventpulse-payment-dev.azurewebsites.net` |

---

## 5. Startup Database Migrations vs. Seeding Behavior Matrix

| Environment | `Database:MigrateOnStartup` | `Database:SeedOnStartup` | Behavior |
| :--- | :---: | :---: | :--- |
| **Local Development** (`Development`) | `true` *(via `appsettings.Development.json`)* | `true` *(via `appsettings.Development.json`)* | Migrations run automatically; test events seeded for immediate offline development. |
| **Azure Dev / Staging** (`Production`) | `true` *(via `Database__MigrateOnStartup=true`)* | `false` *(default)* | EF Core migrations apply automatically upon deployment; NO dummy test events created. |
| **Azure Production** (`Production`) | `true` or via CD pipeline step | `false` | Zero demo data pollution; clean schema updates. |

---

## 6. Pre-Deployment Validation Checklist

- [ ] Azure AD App registration created with OIDC federated credential for GitHub repository `EventPulse`.
- [ ] Role assignment **Contributor** granted to Azure AD App on resource group `rg-eventpulse-dev`.
- [ ] Secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` added to GitHub repository.
- [ ] `pg-eventpulse-dev` Flexible Server firewall rules allow Azure services (`0.0.0.0`).
- [ ] Databases `eventpulse_identity` and `eventpulse_events` created in `pg-eventpulse-dev`.
- [ ] Azure Blob Storage container `event-posters` created with public blob access or CDN integration.
- [ ] Identical `Jwt__Key` configured in both `eventpulse-identity-dev` and `eventpulse-event-dev`.
- [ ] `Database__MigrateOnStartup=true` set on `eventpulse-event-dev`.
- [ ] 4 YARP destination address settings configured on `eventpulse-gateway-dev`.
- [ ] All services pass health check endpoint `/health`.
