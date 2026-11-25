# =============================================================================
# EventPulse Backend - Dev Runner
# Starts: Gateway (:7000)  |  IdentityService (:7101)  |  EventService (:7102)  |  BookingService (:7103)  |  PaymentService (:7104)
#
# Usage:
#   From repo root or backend/:
#     .\backend\dev-start.ps1        (from repo root)
#     .\dev-start.ps1                (from backend/)
#
# Press Ctrl+C to stop all services.
# =============================================================================

$ErrorActionPreference = "Stop"

# -- Tool & Prerequisite Validation -------------------------------------------
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: dotnet CLI was not found in PATH. Please install .NET SDK 10." -ForegroundColor Red
    exit 1
}

# -- Helper: Terminate lingering processes on service ports and EventPulse.* --
function Stop-EventPulseProcesses {
    param([int[]]$Ports = @(7000, 7101, 7102, 7103, 7104))
    
    # 1. Terminate processes listening on target ports
    foreach ($port in $Ports) {
        try {
            $connections = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
            foreach ($conn in $connections) {
                if ($conn.OwningProcess -and $conn.OwningProcess -ne 0 -and $conn.OwningProcess -ne $PID) {
                    try {
                        $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
                        if ($proc) {
                            Write-Host "  Stopping lingering process $($proc.ProcessName) (PID: $($proc.Id)) on port $port..." -ForegroundColor Yellow
                            Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
                        }
                    } catch {}
                }
            }
        } catch {}
    }

    # 2. Terminate any lingering EventPulse.* processes
    try {
        $lingeringProcesses = Get-Process -Name "EventPulse.*" -ErrorAction SilentlyContinue
        foreach ($proc in $lingeringProcesses) {
            if ($proc.Id -ne $PID) {
                try {
                    Write-Host "  Stopping lingering process $($proc.ProcessName) (PID: $($proc.Id))..." -ForegroundColor Yellow
                    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                } catch {}
            }
        }
    } catch {}
}

# -- Pre-startup Cleanup -------------------------------------------------------
Write-Host "Cleaning up any lingering processes on service ports..." -ForegroundColor DarkGray
Stop-EventPulseProcesses -Ports @(7000, 7101, 7102, 7103, 7104)

# -- Load Environment Variables from .env --------------------------------------
$EnvFile = Join-Path $PSScriptRoot ".env"
if (-not (Test-Path $EnvFile)) {
    $EnvFile = Join-Path (Split-Path $PSScriptRoot -Parent) ".env"
}
if (Test-Path $EnvFile) {
    Write-Host "Loading environment variables from $EnvFile..." -ForegroundColor Green
    foreach ($line in Get-Content $EnvFile) {
        if (![string]::IsNullOrWhiteSpace($line) -and !$line.TrimStart().StartsWith("#")) {
            $parts = $line.Split('=', 2)
            if ($parts.Length -eq 2) {
                [Environment]::SetEnvironmentVariable($parts[0].Trim(), $parts[1].Trim())
                Set-Item -Path "env:$($parts[0].Trim())" -Value $parts[1].Trim()
            }
        }
    }
}

# -- Resolve paths relative to THIS script's directory -------------------------
$BackendRoot = $PSScriptRoot
$RepoRoot = Split-Path $BackendRoot -Parent

# -- Check & Start Required Docker Infrastructure (Postgres, Kafka, Azurite) ---
if (Get-Command docker -ErrorAction SilentlyContinue) {
    try {
        $pgContainer = docker ps --filter "name=eventpulse-postgres" --filter "status=running" --format "{{.Names}}" 2>$null
        if (-not $pgContainer -or $pgContainer -notmatch "eventpulse-postgres") {
            Write-Host "Starting Docker infrastructure containers..." -ForegroundColor Yellow
            docker compose up -d postgres kafka azurite 2>&1 | Out-Null
            Start-Sleep -Seconds 2
        }
    } catch {
        Write-Host "Note: Docker daemon check failed or docker compose returned error. Proceeding with local configuration." -ForegroundColor DarkGray
    }
}

# -- Ensure Kafka topics exist if Kafka container is active --------------------
$setupKafkaScript = Join-Path $RepoRoot "scripts\setup-kafka.ps1"
if (Test-Path $setupKafkaScript) {
    try {
        $kafkaContainer = docker ps --filter "name=eventpulse-kafka" --filter "status=running" --format "{{.Names}}" 2>$null
        if ($kafkaContainer -match "eventpulse-kafka") {
            Write-Host "Ensuring Kafka topics are provisioned..." -ForegroundColor DarkGray
            & powershell -ExecutionPolicy Bypass -File $setupKafkaScript | Out-Null
            Write-Host "Kafka topics verified." -ForegroundColor Green
        }
    } catch {
        Write-Host "Note: Kafka container not reachable or docker not active. Skipping auto topic setup." -ForegroundColor DarkGray
    }
}

# -- Single Build of Solution (Prevents CS2012 dll file locking) ---------------
Write-Host ""
Write-Host "Building EventPulse backend solution..." -ForegroundColor Cyan
dotnet build "$BackendRoot\EventPulse.Backend.sln" -c Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nERROR: Solution build failed with exit code $LASTEXITCODE. Aborting startup." -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "Build completed successfully.`n" -ForegroundColor Green

# -- Service Definitions -------------------------------------------------------
$IdentityService = @{
    Name  = "IdentityService"
    Color = "Green"
    Path  = "$BackendRoot\services\identity-service\src\EventPulse.IdentityService"
    Port  = 7101
}

$Microservices = @(
    @{
        Name  = "EventService"
        Color = "Yellow"
        Path  = "$BackendRoot\services\event-service\src\EventPulse.EventService"
        Port  = 7102
    },
    @{
        Name  = "BookingService"
        Color = "Magenta"
        Path  = "$BackendRoot\services\booking-service\src\EventPulse.BookingService"
        Port  = 7103
    },
    @{
        Name  = "PaymentService"
        Color = "Blue"
        Path  = "$BackendRoot\services\payment-service\src\EventPulse.PaymentService"
        Port  = 7104
    }
)

$Gateway = @{
    Name  = "Gateway"
    Color = "Cyan"
    Path  = "$BackendRoot\gateway\src\EventPulse.Gateway"
    Port  = 7000
}

$AllServices = @($IdentityService) + $Microservices + @($Gateway)

# -- Validate project directories exist ----------------------------------------
foreach ($svc in $AllServices) {
    if (-not (Test-Path $svc.Path)) {
        Write-Host "ERROR: Project directory not found: $($svc.Path)" -ForegroundColor Red
        exit 1
    }
}

# -- Helper: Poll Health Endpoint ---------------------------------------------
function Wait-ForServiceHealth {
    param(
        [string]$Name,
        [int]$Port,
        [int]$TimeoutSeconds = 30
    )
    $url = "http://localhost:$Port/health"
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        try {
            $resp = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 300) {
                return $true
            }
        } catch {}
        Start-Sleep -Milliseconds 400
    }
    return $false
}

# -- Print startup banner ------------------------------------------------------
Write-Host ""
Write-Host "  EventPulse Backend Dev Runner" -ForegroundColor White
Write-Host "  ------------------------------------------" -ForegroundColor DarkGray
foreach ($svc in $AllServices) {
    Write-Host "  [$($svc.Name)]" -ForegroundColor $svc.Color -NoNewline
    Write-Host " -> http://localhost:$($svc.Port)" -ForegroundColor DarkGray
}
Write-Host "  ------------------------------------------" -ForegroundColor DarkGray
Write-Host "  Press Ctrl+C to stop all services." -ForegroundColor DarkGray
Write-Host ""

# -- Helper to launch a service as a background job ---------------------------
function Start-ServiceJob {
    param($Service)
    $job = Start-Job -Name $Service.Name -ScriptBlock {
        param($projectPath, $port)
        Set-Location $projectPath
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $env:ASPNETCORE_URLS        = "http://localhost:$port"
        dotnet run --no-build --no-launch-profile 2>&1
    } -ArgumentList $Service.Path, $Service.Port
    return @{ Job = $job; Service = $Service }
}

$Jobs = @()

# Stage 1: Start IdentityService first (executes EF Core migrations and seeds roles)
Write-Host "  Starting [IdentityService] and applying database migrations..." -ForegroundColor Green
$idJobEntry = Start-ServiceJob -Service $IdentityService
$Jobs += $idJobEntry
$idHealthy = Wait-ForServiceHealth -Name $IdentityService.Name -Port $IdentityService.Port -TimeoutSeconds 35
if ($idHealthy) {
    Write-Host "  [IdentityService] is healthy and listening on http://localhost:$($IdentityService.Port)" -ForegroundColor Green
} else {
    Write-Host "  WARNING: [IdentityService] health check timed out. Proceeding..." -ForegroundColor Yellow
}

# Stage 2: Start remaining backend microservices concurrently
foreach ($svc in $Microservices) {
    $entry = Start-ServiceJob -Service $svc
    $Jobs += $entry
    Write-Host "  Started [$($svc.Name)] - job #$($entry.Job.Id)" -ForegroundColor $svc.Color
}

# Wait for microservices health checks
foreach ($svc in $Microservices) {
    $svcHealthy = Wait-ForServiceHealth -Name $svc.Name -Port $svc.Port -TimeoutSeconds 25
    if ($svcHealthy) {
        Write-Host "  [$($svc.Name)] is healthy on http://localhost:$($svc.Port)" -ForegroundColor $svc.Color
    }
}

# Stage 3: Start Gateway once backend services are ready
Write-Host "  Starting [Gateway] reverse proxy..." -ForegroundColor Cyan
$gwJobEntry = Start-ServiceJob -Service $Gateway
$Jobs += $gwJobEntry
$gwHealthy = Wait-ForServiceHealth -Name $Gateway.Name -Port $Gateway.Port -TimeoutSeconds 20
if ($gwHealthy) {
    Write-Host "  [Gateway] is healthy on http://localhost:$($Gateway.Port)" -ForegroundColor Cyan
}

# -- Optional: Stripe Webhook Forwarder ----------------------------------------
if (Get-Command stripe -ErrorAction SilentlyContinue) {
    $stripeJob = Start-Job -Name "StripeListener" -ScriptBlock {
        while ($true) {
            stripe listen --forward-to localhost:7104/api/payments/webhook --events checkout.session.completed,payment_intent.payment_failed 2>&1
            Start-Sleep -Seconds 3
        }
    }
    $Jobs += @{ Job = $stripeJob; Service = @{ Name = "StripeListener"; Color = "DarkYellow" } }
    Write-Host "  Started [StripeListener] - forwarding to http://localhost:7104/api/payments/webhook" -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "  All services initialized. Streaming logs (Press Ctrl+C to stop)..." -ForegroundColor White
Write-Host ""

# -- Stream all job output in one terminal loop --------------------------------
$PrintedLines = @{}
foreach ($j in $Jobs) { $PrintedLines[$j.Job.Id] = 0 }

try {
    while ($true) {
        $anyRunning = $false

        foreach ($entry in $Jobs) {
            $job  = $entry.Job
            $svc  = $entry.Service

            # Check if job is still alive
            if ($job.State -notin @("Completed","Failed","Stopped")) {
                $anyRunning = $true
            }

            # Receive new output lines since last poll
            $output = Receive-Job -Job $job 2>&1
            if ($output) {
                foreach ($line in $output) {
                    $prefix = "[$($svc.Name)]"
                    Write-Host $prefix -ForegroundColor $svc.Color -NoNewline
                    Write-Host " $line"
                }
            }

            # Report if a job died unexpectedly
            if ($job.State -in @("Completed","Failed","Stopped") -and $PrintedLines[$job.Id] -ne -1) {
                Write-Host "[$($svc.Name)] PROCESS EXITED UNEXPECTEDLY (State: $($job.State))" -ForegroundColor Red
                $PrintedLines[$job.Id] = -1
            }
        }

        if (-not $anyRunning) {
            Write-Host "`nAll services have stopped." -ForegroundColor DarkGray
            break
        }

        Start-Sleep -Milliseconds 300
    }
}
finally {
    # -- Cleanup: stop all jobs on Ctrl+C or natural exit ----------------------
    Write-Host "`n  Stopping all services..." -ForegroundColor DarkGray

    foreach ($entry in $Jobs) {
        $job = $entry.Job
        $svc = $entry.Service
        if ($job.State -notin @("Completed","Failed","Stopped")) {
            Stop-Job -Job $job -ErrorAction SilentlyContinue
            Write-Host "  Stopped job [$($svc.Name)]" -ForegroundColor $svc.Color
        }
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
    }

    # Ensure all child dotnet / EventPulse processes are cleanly terminated
    Stop-EventPulseProcesses -Ports @(7000, 7101, 7102, 7103, 7104)
    Get-Process -Name "stripe" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    Write-Host "  Done. All services stopped." -ForegroundColor DarkGray
    Write-Host ""
}
