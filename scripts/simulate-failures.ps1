# ==============================================================================
# Script: simulate-failures.ps1
# Description: Sends 20 rapid requests to the IdentityService failure endpoint
#              to trigger the Prometheus HighHttp5xxRate alert and populate
#              the Grafana error dashboard panels.
# Usage: .\scripts\simulate-failures.ps1
# ==============================================================================

$Endpoint = "http://localhost:7101/api/test/fail-500"
$RequestCount = 20
$DelayMilliseconds = 100

Write-Host "Starting failure simulation..." -ForegroundColor Cyan
Write-Host "Target Endpoint: $Endpoint"
Write-Host "Sending $RequestCount requests with a $DelayMilliseconds ms delay between each..."

for ($i = 1; $i -le $RequestCount; $i++) {
    try {
        # Using -ErrorAction SilentlyContinue because Invoke-RestMethod throws an exception on 500 status codes
        $response = Invoke-RestMethod -Uri $Endpoint -Method Get -ErrorAction Stop
    }
    catch {
        Write-Host "[$i/$RequestCount] Request failed as expected with status code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Yellow
    }
    
    Start-Sleep -Milliseconds $DelayMilliseconds
}

Write-Host "`nSimulation complete!" -ForegroundColor Green
Write-Host "Please check the following:"
Write-Host "1. Grafana Dashboard (Errors panel): http://localhost:3000"
Write-Host "2. Prometheus Alerts: http://localhost:9090/alerts"
