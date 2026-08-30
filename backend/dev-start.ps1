# =============================================================================
# EventPulse Backend — Dev Runner
# Starts: Gateway (:7000)  |  IdentityService (:7101)  |  EventService (:7102)
#
# Usage:
#   From repo root or backend/:
#     .\backend\dev-start.ps1        (from repo root)
#     .\dev-start.ps1                (from backend/)
#
# Press Ctrl+C to stop all services.
# =============================================================================

$ErrorActionPreference = "Stop"

# ── Resolve paths relative to THIS script's directory ─────────────────────────
$BackendRoot = $PSScriptRoot

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

# ── Start each service as a background job ───────────────────────────────────
$Jobs = @()

foreach ($svc in $Services) {
    $job = Start-Job -Name $svc.Name -ScriptBlock {
        param($projectPath, $port)
        Set-Location $projectPath
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $env:ASPNETCORE_URLS        = "http://localhost:$port"
        dotnet run --no-launch-profile 2>&1
    } -ArgumentList $svc.Path, $svc.Port

    $Jobs += @{ Job = $job; Service = $svc }
    Write-Host "  Started [$($svc.Name)] — job #$($job.Id)" -ForegroundColor $svc.Color
}

Write-Host ""

# ── Stream all job output in one terminal loop ────────────────────────────────
# Tracks which lines have already been printed per job (avoids duplicates)
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
            Stop-Job  -Job $job
            Write-Host "  Stopped [$($svc.Name)]" -ForegroundColor $svc.Color
        }
        Remove-Job -Job $job -Force
    }

    Write-Host "  Done. All services stopped." -ForegroundColor DarkGray
    Write-Host ""
}
