# Seed the dedicated local Compose stack. Run from PowerShell 7.
$ErrorActionPreference = 'Stop'
$context = docker context show
if ($LASTEXITCODE) { throw 'Docker context is unavailable.' }
$endpoint = docker context inspect $context --format '{{.Endpoints.docker.Host}}'
if ($LASTEXITCODE -or $endpoint -notmatch '^(npipe:////\./pipe/docker(DesktopLinuxEngine|_engine)|unix:///[^\s]+\.sock)$' -or
    ($env:DOCKER_HOST -and $env:DOCKER_HOST -ne $endpoint)) {
    throw 'The benchmark fixture requires a local Docker socket.'
}

$containerJson = docker inspect --type container eventpulse-perf2-postgres
if ($LASTEXITCODE) { throw 'The isolated benchmark PostgreSQL container is unavailable.' }
$container = @($containerJson | ConvertFrom-Json)[0]
$ports = @($container.NetworkSettings.Ports.'5432/tcp')
if (!$container.State.Running -or $container.Config.Labels.'com.docker.compose.project' -ne 'eventpulse-perf2' -or
    $ports.Count -ne 1 -or $ports[0].HostIp -ne '127.0.0.1' -or $ports[0].HostPort -ne '15433' -or
    !($container.Mounts | Where-Object { $_.Name -eq 'eventpulse_perf2_postgres_data' -and $_.Destination -eq '/var/lib/postgresql/data' })) {
    throw 'PostgreSQL does not match the isolated benchmark project, port, and volume.'
}

Get-Content -LiteralPath (Join-Path $PSScriptRoot 'seed-benchmark.sql') -Raw |
    docker exec -i $container.Id psql -X -U postgres -d eventpulse_events -v ON_ERROR_STOP=1
if ($LASTEXITCODE) { throw "Benchmark fixture failed with psql exit $LASTEXITCODE." }
