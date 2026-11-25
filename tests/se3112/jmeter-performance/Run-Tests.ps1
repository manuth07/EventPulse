# =============================================================================
# Run-Tests.ps1  -  EventPulse JMeter Load Test Runner (Windows PowerShell)
# SE3112 Load and Performance Testing - Member 3
#
# Usage:
#   .\Run-Tests.ps1 [-Profile baseline|normal|higher]
#   .\Run-Tests.ps1 -Profile normal -Host localhost -Port 7000
# =============================================================================
param(
    [ValidateSet("baseline","normal","higher")]
    [string]$Profile = "baseline",

    [string]$TargetHost  = "localhost",
    [int]   $TargetPort  = 7000,
    [string]$TargetProto = "http",
    [string]$JMeterHome  = ""
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$PlanFile  = Join-Path $ScriptDir "eventpulse-events-load-test.jmx"

# Workload profiles
switch ($Profile) {
    "baseline" { $Users=5;  $RampUp=10; $Duration=30; $Think=500 }
    "normal"   { $Users=20; $RampUp=20; $Duration=30; $Think=500 }
    "higher"   { $Users=50; $RampUp=30; $Duration=30; $Think=300 }
}

# Locate JMeter
if ($JMeterHome -ne "") {
    $JMeterBin = Join-Path $JMeterHome "bin\jmeter.bat"
} else {
    $JMeterBin = (Get-Command "jmeter.bat" -ErrorAction SilentlyContinue)?.Source
    if (-not $JMeterBin) { $JMeterBin = (Get-Command "jmeter" -ErrorAction SilentlyContinue)?.Source }
}
if (-not $JMeterBin -or -not (Test-Path $JMeterBin)) {
    Write-Error "JMeter not found. Set -JMeterHome or add JMeter\bin to PATH."
    exit 2
}

$Timestamp  = Get-Date -Format "yyyyMMdd_HHmmss"
$ResultsDir = Join-Path $ScriptDir "results"
$ReportsDir = Join-Path $ScriptDir "reports"
$JtlFile    = Join-Path $ResultsDir "$Profile-$Timestamp.jtl"
$ReportDir  = Join-Path $ReportsDir "$Profile-$Timestamp"

New-Item -ItemType Directory -Force $ResultsDir | Out-Null
New-Item -ItemType Directory -Force $ReportsDir | Out-Null

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  EventPulse JMeter Load Test" -ForegroundColor Cyan
Write-Host "  Profile  : $Profile"
Write-Host "  Target   : ${TargetProto}://${TargetHost}:$TargetPort"
Write-Host "  VUsers   : $Users"
Write-Host "  Ramp-up  : ${RampUp}s"
Write-Host "  Duration : ${Duration}s"
Write-Host "  Think    : ${Think}ms"
Write-Host "  JTL      : $JtlFile"
Write-Host "  Report   : $ReportDir"
Write-Host "============================================================" -ForegroundColor Cyan

# Health check
Write-Host "Health-checking ${TargetProto}://${TargetHost}:${TargetPort}/api/events ..."
$MaxRetries = 10
for ($i = 1; $i -le $MaxRetries; $i++) {
    try {
        $resp = Invoke-WebRequest -Uri "${TargetProto}://${TargetHost}:${TargetPort}/api/events" `
                                  -Method GET -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        if ($resp.StatusCode -eq 200) {
            Write-Host "  [OK] Gateway is responding." -ForegroundColor Green
            break
        }
    } catch { }
    if ($i -eq $MaxRetries) {
        Write-Warning "  Gateway did not respond after $MaxRetries attempts - proceeding anyway."
    } else {
        Write-Host "  Attempt $i/$MaxRetries - waiting 5s..."
        Start-Sleep 5
    }
}

& $JMeterBin -n -t $PlanFile `
    "-Jprotocol=$TargetProto" `
    "-Jhost=$TargetHost" `
    "-Jport=$TargetPort" `
    "-Jusers=$Users" `
    "-Jrampup=$RampUp" `
    "-Jduration=$Duration" `
    "-Jthink_time_ms=$Think" `
    -l $JtlFile -e -o $ReportDir `
    -j (Join-Path $ResultsDir "jmeter-$Profile-$Timestamp.log")

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  Test complete!" -ForegroundColor Green
Write-Host "  JTL results : $JtlFile"
Write-Host "  HTML report : $ReportDir\index.html"
Write-Host "============================================================" -ForegroundColor Green