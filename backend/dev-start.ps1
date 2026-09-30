# =============================================================================
# EventPulse Backend — Dev Runner
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

# ── Helper: Terminate lingering processes on service ports and EventPulse.* ──
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

# ── Pre-startup Cleanup ───────────────────────────────────────────────────────
Write-Host "Cleaning up any lingering processes on service ports..." -ForegroundColor DarkGray
Stop-EventPulseProcesses -Ports @(7000, 7101, 7102, 7103, 7104)

# ── Load Environment Variables from .env ──────────────────────────────────────
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

# ── Resolve paths relative to THIS script's directory ─────────────────────────
$BackendRoot = $PSScriptRoot
$RepoRoot = Split-Path $BackendRoot -Parent

# ── Ensure Kafka topics exist if Kafka container is active ────────────────────
$setupKafkaScript = Join-Path $RepoRoot "scripts\setup-kafka.ps1"
if (Test-Path $setupKafkaScript) {
    try {
        $kafkaContainer = docker ps --filter "name=eventpulse-kafka" --filter "status=running" --format "{{.Names}}" 2>$null
        if ($kafkaContainer -eq "eventpulse-kafka") {
            Write-Host "Ensuring Kafka topics are provisioned..." -ForegroundColor DarkGray
            & powershell -ExecutionPolicy Bypass -File $setupKafkaScript | Out-Null
            Write-Host "Kafka topics verified." -ForegroundColor Green
        }
    } catch {
        Write-Host "Note: Kafka container not reachable or docker not active. Skipping auto topic setup." -ForegroundColor DarkGray
    }
}

# ── Single Build of Solution (Prevents CS2012 dll file locking) ───────────────
Write-Host ""
Write-Host "Building EventPulse backend solution..." -ForegroundColor Cyan
dotnet build "$BackendRoot\EventPulse.Backend.sln" -c Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nERROR: Solution build failed with exit code $LASTEXITCODE. Aborting startup." -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "Build completed successfully.`n" -ForegroundColor Green

$Services = @(
    @{
        Name    = "Gateway"
        Color   = "Cyan"
        Path    = "$BackendRoot\gateway\src\EventPulse.Gateway"
        Port    = 7000
    },
    @{
        Name    = "IdentityService"
        Color   = "Green"
        Path    = "$BackendRoot\services\identity-service\src\EventPulse.IdentityService"
        Port    = 7101
    },
    @{
        Name    = "EventService"
        Color   = "Yellow"
        Path    = "$BackendRoot\services\event-service\src\EventPulse.EventService"
        Port    = 7102
    },
    @{
        Name    = "BookingService"
        Color   = "Magenta"
        Path    = "$BackendRoot\services\booking-service\src\EventPulse.BookingService"
        Port    = 7103
    },
    @{
        Name    = "PaymentService"
        Color   = "Blue"
        Path    = "$BackendRoot\services\payment-service\src\EventPulse.PaymentService"
        Port    = 7104
    }
)

# ── Validate project directories exist ────────────────────────────────────────
foreach ($svc in $Services) {
    if (-not (Test-Path $svc.Path)) {
        Write-Host "ERROR: Project directory not found: $($svc.Path)" -ForegroundColor Red
        exit 1
    }
}

# ── Print startup banner ──────────────────────────────────────────────────────
Write-Host ""
Write-Host "  EventPulse Backend Dev Runner" -ForegroundColor White
Write-Host "  ──────────────────────────────────────────" -ForegroundColor DarkGray
foreach ($svc in $Services) {
    Write-Host "  [$($svc.Name)]" -ForegroundColor $svc.Color -NoNewline
    Write-Host " → http://localhost:$($svc.Port)" -ForegroundColor DarkGray
}
Write-Host "  ──────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host "  Press Ctrl+C to stop all services." -ForegroundColor DarkGray
Write-Host ""

# ── Start each service as a background job with --no-build ────────────────────
$Jobs = @()

foreach ($svc in $Services) {
    $job = Start-Job -Name $svc.Name -ScriptBlock {
        param($projectPath, $port)
        Set-Location $projectPath
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $env:ASPNETCORE_URLS        = "http://localhost:$port"
        dotnet run --no-build --no-launch-profile 2>&1
    } -ArgumentList $svc.Path, $svc.Port

    $Jobs += @{ Job = $job; Service = $svc }
    Write-Host "  Started [$($svc.Name)] — job #$($job.Id)" -ForegroundColor $svc.Color
}

Write-Host ""

# ── Stream all job output in one terminal loop ────────────────────────────────
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
            if ($job.State -eq "Failed" -and $PrintedLines[$job.Id] -ne -1) {
                Write-Host "[$($svc.Name)] PROCESS EXITED (State: $($job.State))" -ForegroundColor Red
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
    # ── Cleanup: stop all jobs on Ctrl+C or natural exit ──────────────────────
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

    Write-Host "  Done. All services stopped." -ForegroundColor DarkGray
    Write-Host ""
}
