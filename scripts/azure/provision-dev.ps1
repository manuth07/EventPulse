<#
.SYNOPSIS
    Idempotent Azure CLI provisioning script for EventPulse development infrastructure.

.DESCRIPTION
    Provisions backend Web Apps on the existing Linux App Service Plan (asp-eventpulse-dev)
    and an Azure Storage Account for event images, without touching or recreating existing
    resources (Identity Service, PostgreSQL Flexible Server, App Service Plan, Resource Group).

    Target Web Apps:
      1. eventpulse-event-dev
      2. eventpulse-booking-dev
      3. eventpulse-payment-dev
      4. eventpulse-gateway-dev

    Target Storage:
      - Storage Account: steventpulsedev (or user-specified unique name)
      - Blob Container: event-posters

    SAFETY & IDEMPOTENCY:
      - Validates prerequisites (Azure CLI, active login, subscription).
      - Verifies that required existing resources exist.
      - Checks every resource before creation ([CHECK]).
      - Reuses existing resources without failure ([EXISTS]).
      - Only creates missing resources ([CREATE] -> [DONE]).
      - Configures platform baselines (HTTPS-only, TLS 1.2, .NET 10).
      - NEVER touches or recreates eventpulse-identity-dev.
      - NEVER hardcodes secrets, passwords, or connection strings.
      - Prints required application settings and next steps at the end.

.PARAMETER ResourceGroupName
    Name of the existing Resource Group. Default: 'rg-eventpulse-dev'

.PARAMETER Location
    Azure region for newly created resources. Default: 'southeastasia'

.PARAMETER AppServicePlanName
    Name of the existing Linux App Service Plan. Default: 'asp-eventpulse-dev'

.PARAMETER StorageAccountName
    Optional custom globally unique name for the Azure Storage Account.
    If omitted, defaults to 'steventpulsedev' or appends a deterministic suffix if taken.

.PARAMETER ContainerName
    Name of the blob container for event images. Default: 'event-posters'

.PARAMETER EnablePublicBlobAccess
    Switch to allow anonymous public read access to blobs in the container.
    Default is $false (private access).
    NOTE: The current Event Service (AzureBlobEventImageStorage.cs) generates
    direct blob URLs for public web visitors. If your architecture relies on
    direct anonymous image reads (without SAS tokens or CDN), set this to $true.

.EXAMPLE
    .\scripts\azure\provision-dev.ps1

.EXAMPLE
    .\scripts\azure\provision-dev.ps1 -StorageAccountName "steventpulsedev01" -EnablePublicBlobAccess
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$ResourceGroupName = "rg-eventpulse-dev",

    [Parameter()]
    [string]$Location = "southeastasia",

    [Parameter()]
    [string]$AppServicePlanName = "asp-eventpulse-dev",

    [Parameter()]
    [string]$StorageAccountName = "",

    [Parameter()]
    [string]$ContainerName = "event-posters",

    [Parameter()]
    [switch]$EnablePublicBlobAccess = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ==============================================================================
# Helper Output Functions
# ==============================================================================
function Write-Step   ([string]$msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Write-Check  ([string]$msg) { Write-Host "  [CHECK]  $msg" -ForegroundColor Gray }
function Write-Exists ([string]$msg) { Write-Host "  [EXISTS] $msg" -ForegroundColor Green }
function Write-Create ([string]$msg) { Write-Host "  [CREATE] $msg" -ForegroundColor Yellow }
function Write-Done   ([string]$msg) { Write-Host "  [DONE]   $msg" -ForegroundColor Green }
function Write-Warn   ([string]$msg) { Write-Host "  [WARN]   $msg" -ForegroundColor Magenta }
function Write-Err    ([string]$msg) { Write-Host "  [ERROR]  $msg" -ForegroundColor Red }

# ==============================================================================
# 1. Prerequisite Validation
# ==============================================================================
Write-Step "1. Validating Azure CLI Environment"

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Err "Azure CLI ('az') is not installed or not in PATH."
    Write-Host "Please install Azure CLI from https://aka.ms/installazurecliwindows and retry." -ForegroundColor Yellow
    exit 1
}
Write-Done "Azure CLI is installed: $((az --version 2>&1 | Select-Object -First 1))"

# Verify Azure authentication
Write-Check "Verifying active Azure login session..."
$accountJson = az account show --output json 2>$null
if (-not $accountJson) {
    Write-Err "Not logged in to Azure CLI. Please run 'az login' before running this script."
    exit 1
}

$currentAccount = $accountJson | ConvertFrom-Json
$subId   = $currentAccount.id
$subName = $currentAccount.name
Write-Done "Authenticated to subscription: '$subName' ($subId)"

# ==============================================================================
# 2. Verify Existing Shared Infrastructure (Do NOT create or recreate)
# ==============================================================================
Write-Step "2. Verifying Existing Shared Resources (Read-Only Checks)"

# Resource Group
Write-Check "Checking Resource Group '$ResourceGroupName'..."
$rgExists = az group exists --name $ResourceGroupName --output tsv
if ($rgExists -ne "true") {
    Write-Err "Required Resource Group '$ResourceGroupName' does not exist in subscription '$subName'."
    Write-Host "Per instructions, this script reuses existing infrastructure. Please verify subscription." -ForegroundColor Yellow
    exit 1
}
Write-Exists "Resource Group '$ResourceGroupName' verified."

# App Service Plan
Write-Check "Checking Linux App Service Plan '$AppServicePlanName'..."
$aspJson = az appservice plan show --name $AppServicePlanName --resource-group $ResourceGroupName --output json 2>$null
if (-not $aspJson) {
    Write-Err "Required App Service Plan '$AppServicePlanName' does not exist in '$ResourceGroupName'."
    exit 1
}
$asp = $aspJson | ConvertFrom-Json
$osType = if ($asp.reserved) { 'Linux' } else { 'Windows' }
Write-Exists "App Service Plan '$AppServicePlanName' verified (OS: $osType, SKU: $($asp.sku.name))."

# Identity Service (sanity check - must NOT be touched)
Write-Check "Checking existing Identity Service 'eventpulse-identity-dev'..."
$identityJson = az webapp show --name "eventpulse-identity-dev" --resource-group $ResourceGroupName --output json 2>$null
if ($identityJson) {
    $identity = $identityJson | ConvertFrom-Json
    Write-Exists "Identity Service 'eventpulse-identity-dev' is active at https://$($identity.defaultHostName) (Leaving untouched)."
} else {
    Write-Warn "Identity Service 'eventpulse-identity-dev' not found in '$ResourceGroupName'. (Script will NOT create it)."
}

# PostgreSQL Server (sanity check)
Write-Check "Checking existing PostgreSQL Flexible Server 'pg-eventpulse-dev'..."
$pgJson = az postgres flexible-server show --name "pg-eventpulse-dev" --resource-group $ResourceGroupName --output json 2>$null
if ($pgJson) {
    $pg = $pgJson | ConvertFrom-Json
    Write-Exists "PostgreSQL Flexible Server 'pg-eventpulse-dev' is active ($($pg.fullyQualifiedDomainName))."
} else {
    Write-Warn "PostgreSQL Flexible Server 'pg-eventpulse-dev' was not found or accessible via current CLI credentials."
}

# ==============================================================================
# 3. Provision Backend Web Apps on Existing App Service Plan
# ==============================================================================
Write-Step "3. Provisioning Backend Web Apps (.NET 10 on Linux)"

$backendApps = @(
    @{ Name = "eventpulse-event-dev";   Description = "Event Service (Catalog, Submissions, Reviews)" },
    @{ Name = "eventpulse-booking-dev"; Description = "Booking Service (Reservations, Seat Management)" },
    @{ Name = "eventpulse-payment-dev"; Description = "Payment Service (Transactions, Gateways)" },
    @{ Name = "eventpulse-gateway-dev"; Description = "API Gateway (YARP Reverse Proxy & Routing)" }
)

$provisionedApps = @()

foreach ($app in $backendApps) {
    $appName = $app.Name
    $appDesc = $app.Description

    Write-Host "`n-- $appName ($appDesc) --" -ForegroundColor White
    Write-Check "Checking if Web App '$appName' exists in '$ResourceGroupName'..."

    $existingAppJson = az webapp show --name $appName --resource-group $ResourceGroupName --output json 2>$null

    if ($existingAppJson) {
        $appObj = $existingAppJson | ConvertFrom-Json
        Write-Exists "Web App '$appName' already exists. Reusing existing resource."
        $defaultHost = $appObj.defaultHostName
    } else {
        Write-Create "Creating Web App '$appName' on plan '$AppServicePlanName' with .NET 10 (Linux)..."

        # Create web app with Linux .NET 10 runtime
        $createJson = az webapp create `
            --name $appName `
            --resource-group $ResourceGroupName `
            --plan $AppServicePlanName `
            --runtime "DOTNETCORE:10.0" `
            --output json

        if ($LASTEXITCODE -ne 0) {
            Write-Err "Failed to create Web App '$appName'."
            exit $LASTEXITCODE
        }

        $appObj = $createJson | ConvertFrom-Json
        $defaultHost = $appObj.defaultHostName
        Write-Done "Web App '$appName' created successfully."
    }

    # Ensure baseline platform configuration (non-secret security & runtime settings)
    Write-Check "Enforcing security baseline (HTTPS-only, TLS 1.2, LinuxFxVersion=DOTNETCORE|10.0)..."

    az webapp update `
        --name $appName `
        --resource-group $ResourceGroupName `
        --https-only true `
        --output none

    az webapp config set `
        --name $appName `
        --resource-group $ResourceGroupName `
        --min-tls-version "1.2" `
        --ftps-state "Disabled" `
        --linux-fx-version "DOTNETCORE|10.0" `
        --output none

    Write-Done "Platform baseline configured for '$appName'."

    $provisionedApps += [PSCustomObject]@{
        Name        = $appName
        Description = $appDesc
        HostName    = "https://$defaultHost"
    }
}

# ==============================================================================
# 4. Provision Azure Blob Storage for Event Images
# ==============================================================================
Write-Step "4. Provisioning Azure Blob Storage for EventPulse Images"

# Determine a globally unique Storage Account Name if not explicitly supplied
if ([string]::IsNullOrWhiteSpace($StorageAccountName)) {
    $candidate = "steventpulsedev"
    Write-Check "Checking availability for candidate Storage Account name '$candidate'..."
    $checkResult = az storage account check-name --name $candidate --output json | ConvertFrom-Json

    if ($checkResult.nameAvailable) {
        $StorageAccountName = $candidate
    } else {
        # Check if this exact storage account already belongs to our resource group
        $ownedCheck = az storage account show --name $candidate --resource-group $ResourceGroupName --output json 2>$null
        if ($ownedCheck) {
            $StorageAccountName = $candidate
        } else {
            # Derive deterministic 5-char hash from subscription ID + resource group
            $md5 = [System.Security.Cryptography.MD5]::Create()
            $hashBytes = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes("$subId$ResourceGroupName"))
            $suffix = ([System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLower()).Substring(0, 5)
            $StorageAccountName = "steventpulse$suffix"
            Write-Check "Candidate '$candidate' in use globally. Using generated name '$StorageAccountName'..."
        }
    }
}

$StorageAccountName = $StorageAccountName.ToLower().Trim()
Write-Host "Target Storage Account: '$StorageAccountName'" -ForegroundColor White

# Check if Storage Account exists in this Resource Group
$existingStorageJson = az storage account show --name $StorageAccountName --resource-group $ResourceGroupName --output json 2>$null

if ($existingStorageJson) {
    Write-Exists "Storage Account '$StorageAccountName' already exists in '$ResourceGroupName'. Reusing existing account."
} else {
    Write-Create "Creating Storage Account '$StorageAccountName' (SKU: Standard_LRS, Kind: StorageV2, Location: '$Location')..."

    # Configure public blob access according to architecture requirements
    $allowPublicArg = if ($EnablePublicBlobAccess) { "true" } else { "false" }

    az storage account create `
        --name $StorageAccountName `
        --resource-group $ResourceGroupName `
        --location $Location `
        --sku Standard_LRS `
        --kind StorageV2 `
        --min-tls-version TLS1_2 `
        --allow-blob-public-access $allowPublicArg `
        --output none

    if ($LASTEXITCODE -ne 0) {
        Write-Err "Failed to create Storage Account '$StorageAccountName'."
        exit $LASTEXITCODE
    }
    Write-Done "Storage Account '$StorageAccountName' created successfully."
}

# ------------------------------------------------------------------------------
# Create Blob Container: 'event-posters'
# ------------------------------------------------------------------------------
Write-Check "Checking Blob Container '$ContainerName' in '$StorageAccountName'..."

# Retrieve storage connection string dynamically for container management
$storageConnStr = (az storage account show-connection-string `
    --name $StorageAccountName `
    --resource-group $ResourceGroupName `
    --query connectionString `
    --output tsv)

$containerExists = az storage container exists `
    --name $ContainerName `
    --connection-string $storageConnStr `
    --query exists `
    --output tsv

if ($containerExists -eq "true") {
    Write-Exists "Blob Container '$ContainerName' already exists. Reusing existing container."
} else {
    # Determine container public access mode
    # NOTE:
    # EventPulse's AzureBlobEventImageStorage.cs currently builds public URLs:
    #   https://<account>.blob.core.windows.net/event-posters/<blobName>
    # If $EnablePublicBlobAccess is set, we configure '--public-access blob' so
    # individual blobs can be loaded directly by frontend browsers without SAS tokens.
    # Otherwise, default to private access ('off') per strict cloud security defaults.
    $publicAccessMode = if ($EnablePublicBlobAccess) { "blob" } else { "off" }

    Write-Create "Creating Blob Container '$ContainerName' (public-access: $publicAccessMode)..."

    az storage container create `
        --name $ContainerName `
        --connection-string $storageConnStr `
        --public-access $publicAccessMode `
        --output none

    if ($LASTEXITCODE -ne 0) {
        Write-Err "Failed to create container '$ContainerName'."
        exit $LASTEXITCODE
    }
    Write-Done "Blob Container '$ContainerName' created."
}

if (-not $EnablePublicBlobAccess) {
    Write-Warn "Container '$ContainerName' is set to PRIVATE access."
    Write-Host "         The current EventPulse Event Service (AzureBlobEventImageStorage.cs) uses direct unauthenticated" -ForegroundColor DarkGray
    Write-Host "         URLs for posters. If public browsers fail to load posters, re-run with -EnablePublicBlobAccess" -ForegroundColor DarkGray
    Write-Host "         or execute: az storage container set-permission --name $ContainerName --public-access blob --connection-string `"<conn-str>`"" -ForegroundColor DarkGray
}

# ==============================================================================
# 5. Infrastructure Summary & Outputs
# ==============================================================================
Write-Step "5. Infrastructure Provisioning Summary"

Write-Host "Resource Group:    $ResourceGroupName" -ForegroundColor White
Write-Host "App Service Plan:  $AppServicePlanName (Linux)" -ForegroundColor White
Write-Host "Region:            $Location" -ForegroundColor White
Write-Host "Storage Account:   $StorageAccountName" -ForegroundColor White
Write-Host "Blob Container:    $ContainerName" -ForegroundColor White
Write-Host ""
Write-Host "Provisioned Web Apps:" -ForegroundColor White
Write-Host "-------------------------------------------------------------------------------" -ForegroundColor Gray
Write-Host ("{0,-28} {1,-45}" -f "SERVICE NAME", "DEFAULT URL") -ForegroundColor Cyan
Write-Host ("{0,-28} {1,-45}" -f "eventpulse-identity-dev", "https://eventpulse-identity-dev.azurewebsites.net (pre-existing)") -ForegroundColor Gray
foreach ($app in $provisionedApps) {
    Write-Host ("{0,-28} {1,-45}" -f $app.Name, $app.HostName) -ForegroundColor Green
}
Write-Host "-------------------------------------------------------------------------------" -ForegroundColor Gray

# ==============================================================================
# 6. Next Manual Steps & Configuration Guide
# ==============================================================================
Write-Step "6. REQUIRED NEXT STEPS (Application Configuration & Secrets)"

Write-Host @"
The infrastructure has been provisioned, but backend services require application settings
and secrets before they can run in production.

DO NOT commit secrets to git. Configure these in Azure App Service Configuration:
  (Azure Portal -> Web App -> Settings -> Environment variables / Configuration)
  or via 'az webapp config appsettings set --name <app> --resource-group $ResourceGroupName --settings KEY=VALUE'

================================================================================
1. EVENT SERVICE (eventpulse-event-dev)
================================================================================
Settings to configure:
  - ConnectionStrings__EventDatabase
      Format: "Host=pg-eventpulse-dev.postgres.database.azure.com;Port=5432;Database=eventpulse_events;Username=<pg-admin>;Password=<pg-password>;Ssl Mode=Require;"
  - Jwt__Issuer
      Value:  "EventPulse.IdentityService"
  - Jwt__Audience
      Value:  "EventPulse.Clients"
  - Jwt__Key
      Value:  "<Same symmetric 256-bit signing key configured in eventpulse-identity-dev>"
  - BlobStorage__ConnectionString
      Value:  "<Azure Storage Account Connection String for '$StorageAccountName'>"
      Retrieve via:
        az storage account show-connection-string --name $StorageAccountName --resource-group $ResourceGroupName --query connectionString -o tsv
  - BlobStorage__ContainerName
      Value:  "$ContainerName"

================================================================================
2. BOOKING SERVICE (eventpulse-booking-dev)
================================================================================
Current Architecture Note:
  The Booking Service currently does NOT have EF Core database or JWT validation
  registered in Program.cs. It only runs controllers and health checks (/health).
Settings to configure:
  - ASPNETCORE_ENVIRONMENT = "Development" (or "Production")
  - (No database connection string required until Booking Service adds EF Core persistence)

================================================================================
3. PAYMENT SERVICE (eventpulse-payment-dev)
================================================================================
Current Architecture Note:
  The Payment Service currently does NOT have EF Core database or JWT validation
  registered in Program.cs. It only runs controllers and health checks (/health).
Settings to configure:
  - ASPNETCORE_ENVIRONMENT = "Development" (or "Production")
  - (No database connection string required until Payment Service adds EF Core persistence)

================================================================================
4. GATEWAY (eventpulse-gateway-dev - YARP Reverse Proxy)
================================================================================
Configure downstream service cluster destinations in Gateway App Settings:
  - ReverseProxy__Clusters__identity_cluster__Destinations__identity_service__Address
      Value: "https://eventpulse-identity-dev.azurewebsites.net"
  - ReverseProxy__Clusters__event_cluster__Destinations__event_service__Address
      Value: "https://eventpulse-event-dev.azurewebsites.net"
  - ReverseProxy__Clusters__booking_cluster__Destinations__booking_service__Address
      Value: "https://eventpulse-booking-dev.azurewebsites.net"
  - ReverseProxy__Clusters__payment_cluster__Destinations__payment_service__Address
      Value: "https://eventpulse-payment-dev.azurewebsites.net"

================================================================================
5. VERIFICATION AFTER DEPLOYMENT
================================================================================
Test health checks for all services:
  curl -k https://eventpulse-identity-dev.azurewebsites.net/health
  curl -k https://eventpulse-event-dev.azurewebsites.net/health
  curl -k https://eventpulse-booking-dev.azurewebsites.net/health
  curl -k https://eventpulse-payment-dev.azurewebsites.net/health
  curl -k https://eventpulse-gateway-dev.azurewebsites.net/health

"@ -ForegroundColor Yellow
