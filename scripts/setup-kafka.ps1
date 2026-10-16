# =============================================================================
# EventPulse — Kafka Topics Setup Script
# Idempotently provisions required Kafka topics using docker exec.
#
# Usage:
#   .\scripts\setup-kafka.ps1
# =============================================================================

[CmdletBinding()]
param(
    [string]$ContainerName = "eventpulse-kafka",
    [string]$BootstrapServer = "localhost:9092",
    [int]$Partitions = 1,
    [int]$ReplicationFactor = 1
)

$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  EventPulse Kafka Topic Provisioning" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Verify Docker container is running
$runningContainer = docker ps --filter "name=$ContainerName" --filter "status=running" --format "{{.Names}}" 2>$null
if (-not $runningContainer -or $runningContainer.Trim() -ne $ContainerName) {
    Write-Host "ERROR: Kafka container '$ContainerName' is not running." -ForegroundColor Red
    Write-Host "Please start the infrastructure stack first:" -ForegroundColor Yellow
    Write-Host "  docker compose up -d" -ForegroundColor Yellow
    exit 1
}

Write-Host "Connected to Kafka container: $ContainerName" -ForegroundColor Green

# 2. Define required topics
$RequiredTopics = @(
    "payment-succeeded",
    "payment-failed",
    "payment-succeeded-dlq",
    "payment-failed-dlq",
    "booking-refund-requested",
    "payment-refunded",
    "booking-created",
    "event-submitted",
    "event-submitted-dlq"
)

# 3. Create topics idempotently
Write-Host "`nEnsuring required topics exist..." -ForegroundColor DarkGray

foreach ($topic in $RequiredTopics) {
    Write-Host "  Checking/creating topic: $topic" -ForegroundColor Yellow -NoNewline
    $result = docker exec $ContainerName /opt/kafka/bin/kafka-topics.sh `
        --bootstrap-server $BootstrapServer `
        --create `
        --if-not-exists `
        --topic $topic `
        --partitions $Partitions `
        --replication-factor $ReplicationFactor 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host " [FAILED]" -ForegroundColor Red
        Write-Host "    $result" -ForegroundColor Red
        exit $LASTEXITCODE
    } else {
        if ($result -match "Created topic") {
            Write-Host " [CREATED]" -ForegroundColor Green
        } else {
            Write-Host " [OK - Exists]" -ForegroundColor DarkGray
        }
    }
}

# 4. List all current topics
Write-Host "`nCurrent topics in Kafka cluster:" -ForegroundColor Cyan
docker exec $ContainerName /opt/kafka/bin/kafka-topics.sh --bootstrap-server $BootstrapServer --list | ForEach-Object {
    Write-Host "  - $_" -ForegroundColor White
}

Write-Host "`nKafka topics provisioning complete!" -ForegroundColor Green
