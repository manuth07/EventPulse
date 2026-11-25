# EventPulse SE3112 JMeter performance testing

This plan measures the public Events list through the local YARP gateway. It sends one paced GET per iteration and checks the status, JSON media type, and event array. Empty arrays pass the functional check. Use a stable set of future-dated published events when benchmarking a representative response.

## Requirements

- Apache JMeter **5.6.3** and Java **17**. Verify the downloaded archive against Apache's SHA-512 before extracting it. Set `JMETER_HOME` to the extracted directory or put its `bin` directory on PATH. On Windows, set `JAVA_HOME` to the Java 17 JDK. The PowerShell runner puts that JDK first on PATH because the Windows JMeter launcher resolves `java.exe` there. Java 24 caused the bundled Groovy compiler to reject the assertion during verification.
- Python **3.10+** with the standard library. PowerShell uses python and Bash uses python3. Set PYTHON_BIN to an executable path to override either.
- PowerShell **7** on Windows or Bash on Linux, macOS, or WSL.
- An approved isolated EventPulse environment with .NET 10, YARP (port 7000), Event Service (port 7102), and PostgreSQL (host port 15433 with the benchmark overlay). The overlay also provides Kafka and Azurite. Development startup can apply migrations, so use the isolated development database.

## Start the isolated benchmark environment

Run from the repository root. Keep ports `7000`, `7102`, `15433`, `19092`, and `11000` free. The overlay uses separate named volumes and binds to loopback. The Windows run used Docker Compose 5.1.0. Start only the services needed for the Events request path. Event Service uses PostgreSQL and runs Kafka background consumers. Azurite supplies development storage configuration. Identity, Booking, Payment, and the frontend are unnecessary for this request.

```powershell
$overlay = 'tests/se3112/jmeter-performance/local-benchmark.compose.yml'
docker compose -p eventpulse-perf2 -f docker-compose.yml -f $overlay up -d postgres kafka azurite
docker exec eventpulse-perf2-postgres pg_isready -h 127.0.0.1
docker exec eventpulse-perf2-kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server 127.0.0.1:19092 --list
foreach ($topic in 'event-submitted','event-submitted-dlq','booking-cancelled') {
    docker exec eventpulse-perf2-kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server 127.0.0.1:19092 --create --if-not-exists --topic $topic --partitions 1 --replication-factor 1
}
dotnet build backend/services/event-service/src/EventPulse.EventService -c Release
dotnet build backend/gateway/src/EventPulse.Gateway -c Release
```

Wait for PostgreSQL and Kafka before proceeding. Use the repository's default development Compose credentials for this project. In a PowerShell terminal, configure and start the Event Service using its project directory as the working directory:

```powershell
$event = 'backend/services/event-service/src/EventPulse.EventService'
$defaults = Get-Content "$event/appsettings.json" -Raw | ConvertFrom-Json
$development = Get-Content "$event/appsettings.Development.json" -Raw | ConvertFrom-Json
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ConnectionStrings__EventDatabase = $defaults.ConnectionStrings.EventDatabase.Replace('Host=localhost;Port=5433', 'Host=127.0.0.1;Port=15433')
if ($env:ConnectionStrings__EventDatabase -notmatch '^Host=127\.0\.0\.1;Port=15433;Database=eventpulse_events;') { throw 'Expected isolated database' }
$env:Kafka__BootstrapServers = '127.0.0.1:19092'
$env:BlobStorage__ConnectionString = $development.BlobStorage.ConnectionString.Replace(':10000/', ':11000/')
Push-Location $event
dotnet bin/Release/net10.0/EventPulse.EventService.dll --urls http://127.0.0.1:7102
```

In a separate terminal from the repository root, start the gateway. The explicit IPv4 addresses avoid the local IPv6 resolution delays observed during startup.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ReverseProxy__Clusters__event_cluster__Destinations__event_service__Address = 'http://127.0.0.1:7102'
Push-Location backend/gateway/src/EventPulse.Gateway
dotnet bin/Release/net10.0/EventPulse.Gateway.dll --urls http://127.0.0.1:7000
```

## Verify the local target

From the repository root, with the approved local environment already running:

```text
curl -i http://localhost:7000/health
curl -i -H "Accept: application/json" http://localhost:7000/api/events
curl -i http://localhost:7102/health
curl -i -H "Accept: application/json" http://localhost:7102/api/events
```

Use curl.exe on Windows if curl resolves to a PowerShell alias. Expect HTTP 200 at all four URLs and `application/json` with an array at both Events URLs. The runners check the gateway health and Events URLs automatically and fail if readiness expires, including if the server redirects.

## Seed the 100-event assessment fixture

Keep the empty-dataset evidence before seeding. With the isolated stack running and migrations applied, run from the repository root:

```powershell
pwsh -NoProfile -File tests/se3112/jmeter-performance/Seed-Benchmark.ps1
$evidence = 'tests/se3112/jmeter-performance/results/phase3'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
curl.exe -fsS -o (Join-Path $evidence 'populated-events.json') -w '%{http_code} %{content_type} %{size_download}' http://localhost:7000/api/events
```

The script only accepts the dedicated `eventpulse-perf2` PostgreSQL container, its local socket, loopback mapping, and named volume. The SQL runs in one transaction. It uses fixed synthetic IDs, scheduled dates from January to April 2028, valid categories, and a General Admission ticket with capacity 100 for each event. It sets the event to Published only after its ticket exists. Organizer and reviewer IDs are synthetic identifiers in the Event database and have no Identity Service foreign key. A second run adds zero events and zero tickets. It refuses to mix with unrelated visible events or seed expired dates. No API writes or extra credentials are required by this fixture.

For a populated comparison, run `-Profile smoke` once as an unmeasured warm-up, then run smoke, baseline, normal, and higher sequentially with a separate `-OutputRoot`. Verify the count and resource headroom before each run. Preserve the original artifact directory. The recorded series used `results/phase3/warmup` and `results/phase3/assignment`.

## Workloads

| Profile | Virtual users | Ramp-up | Scheduled duration | Delay before each request |
|---|---:|---:|---:|---:|
| smoke | 1 | 1 s | 10 s | 500 ms |
| baseline (default) | 5 | 10 s | 30 s | 500 ms |
| normal | 20 | 20 s | 30 s | 500 ms |
| higher | 50 | 30 s | 30 s | 500 ms |

One virtual user is one thread. The Thread Group loops until its scheduler ends. **Scheduled duration includes ramp-up.** The baseline has roughly 20 seconds at target concurrency, normal roughly 10, and higher very little. In-flight HTTP requests can outlast the scheduled cutoff. For a separate experiment with about 30 seconds at full load, use scheduled durations of 40, 50, and 60 seconds respectively. Keep 500 ms pacing and the same fixture and warm-up conditions for comparisons.

## Run locally

From the repository root, use one of these commands. Substitute baseline, normal, or higher for smoke to select that profile.

```powershell
.\tests\se3112\jmeter-performance\Run-Tests.ps1 -Profile smoke
.\tests\se3112\jmeter-performance\Run-Tests.ps1 -Profile baseline
.\tests\se3112\jmeter-performance\Run-Tests.ps1 -Profile normal
.\tests\se3112\jmeter-performance\Run-Tests.ps1 -Profile higher
.\tests\se3112\jmeter-performance\Run-Tests.ps1 -Profile higher -Duration 60
```

```bash
bash tests/se3112/jmeter-performance/run-tests.sh smoke
DURATION=60 bash tests/se3112/jmeter-performance/run-tests.sh higher
```

| Setting | PowerShell argument | Bash environment variable | JMeter property |
|---|---|---|---|
| Protocol | -TargetProto | TARGET_PROTO | protocol |
| Host | -TargetHost | TARGET_HOST | host |
| Port | -TargetPort | TARGET_PORT | port |
| API path | -ApiPath | API_PATH | api_path |
| Virtual users | -Users | USERS | users |
| Ramp-up seconds | -RampUp | RAMPUP | rampup |
| Scheduled seconds | -Duration | DURATION | duration |
| Pacing milliseconds | -ThinkTimeMs | THINK_TIME_MS | think_time_ms |

The underlying JMX accepts these property names with JMeter's -J flag. Its defaults match the baseline against http://localhost:7000/api/events. Use the runners to get the readiness and results gates. The path override accepts a relative URL path beginning with one slash. A nonlocal host requires prior target-owner approval and an explicit runner opt-in through -AllowNonLocal or ALLOW_NONLOCAL=1. Run assignment workloads only against the approved isolated environment.

Each run prints its unique output directory under tests/se3112/jmeter-performance/results. Override that root with -OutputRoot or OUTPUT_ROOT. The run directory contains results.jtl, jmeter.log, and html/index.html when generated. Keep available artifacts after a failure. The runner returns nonzero on readiness failure, a nonzero JMeter exit, missing or malformed results, zero samples, unexpected HTTP status or assertion failures, or a missing dashboard. The functional gate allows **zero** unexpected failed samples. The assignment has no agreed latency or throughput threshold.

## GitHub Actions handoff

On the group branch, open `.github/workflows/se3112-testing.yml`. Append the block beginning with the two-space-indented `jmeter-performance:` from [jmeter-performance-job.yml](jmeter-performance-job.yml) as the fourth key under `jobs:`. Keep `code-quality`, `unit-testing`, and `selenium-e2e` at the same indentation. No new triggers, secrets, `needs` relationship, or GitHub permissions are required. Keep the existing `contents: read` permission. The cached workflow and appended template parse together with all four jobs. The job pins JMeter 5.6.3 and checks its Apache SHA-512, sets up Java 17, Python, and .NET 10, builds the required projects, and starts PostgreSQL, Kafka, Azurite, Event Service, and YARP on a hosted runner. It provisions the Event Service Kafka topics and runs this command against loopback:

```bash
bash tests/se3112/jmeter-performance/run-tests.sh baseline
```

The job passes only when readiness succeeds, JMeter exits successfully, the dashboard exists, and the CSV validator finds at least one sample and zero unexpected failures. `always()` steps capture available logs, clean up the hosted runner's isolated processes and Compose resources, and upload `jmeter-baseline`. The hosted baseline is a functional load check on the workflow's development data. Use workflow dispatch on the group branch to verify success. In a disposable workflow branch, point only the JMeter test step at an unused loopback port to verify that readiness fails and available backend logs are retained. Restore the correct port before merging that branch. GitHub Actions execution has not been verified.

## Results and limitations

### Empty dataset (Phase 2)

Measured on 25 November 2026 with JMeter 5.6.3 and Java 17.0.12 on the isolated Windows 11 stack. Both Events URLs returned `200 application/json` and `[]`. The smoke warmed up the same backend used for all three assignment profiles. Each row was cross-checked against the JTL, dashboard statistics, and JMeter log. Time values for individual HTTP samples are in milliseconds.

| Profile | Max active threads | Engine seconds | Requests (pass/fail) | Mean | Median | p90 | p95 | Min | Max | Req/s | Error |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Smoke | 1 | 9.909 | 17 / 0 | 7.824 | 5 | 15.6 | 50 | 4 | 50 | 1.827 | 0% |
| Baseline | 5 | 29.690 | 247 / 0 | 3.927 | 4 | 5 | 5 | 2 | 60 | 8.504 | 0% |
| Normal | 20 | 29.990 | 792 / 0 | 4.003 | 3 | 5 | 7 | 1 | 109 | 26.975 | 0% |
| Higher | 50 | 30.013 | 1481 / 0 | 3.405 | 3 | 4 | 5 | 2 | 46 | 50.378 | 0% |

From Baseline to Higher, throughput increased from 8.504 to 50.378 requests/s. The p95 stayed between 5 and 7 ms, and all samples passed. The results describe a small, empty response on a single warmed-up local stack. Higher reached 50 active threads but spent very little time there. Optional extended runs were skipped after sampled available memory reached 473 MiB.

[benchmark-evidence.json](benchmark-evidence.json) records run timestamps, working-tree provenance, dataset size, hashes, and each artifact directory. Local raw results are under `results/phase2/assignment/` and the controlled negative run is under `results/phase2/negative/`. Each run contains its JTL, `jmeter.log`, and `html/statistics.json` plus the dashboard. API probes, backend logs, and the host monitor are under `results/phase2/`. Track the compact manifest, README, and Compose overlay. Keep generated JTLs, logs, and dashboards local or attach available reports as Actions artifacts. The older files in `tests/performance-jmeter/` describe different plans and workloads.

A separate loopback mock returned valid nonempty events and then cycled through invalid responses. JMeter accepted 3 samples and marked 13 as failed, covering malformed JSON, unexpected event shape, HTML content type, an unfollowed HTTP 302, and HTTP 503. The runner exited 1. Separate readiness and result checks rejected a refused connection and missing and malformed JTLs.

### Populated dataset (Phase 3)

The same isolated stack and Java 17 toolchain returned exactly 100 events. Both gateway and direct Event Service responses returned identical 40,501-byte JSON bodies. The empty body measured 2 bytes. Response sizes here are body bytes, excluding HTTP headers. A separate 16-sample smoke warm-up passed before the measured series. The original 2,537 empty-dataset requests and their artifact hashes were preserved.

| Profile | Max active / configured | Engine s | Requests (pass / fail) | Mean ms | Median | p90 | p95 | Min / max | Req/s | Error | Min free MiB / peak CPU |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Smoke | 1 / 1 | 9.537 | 14 / 0 | 10.929 | 6 | 43 | 80 | 5 / 80 | 1.578 | 0% | 583 / 97% |
| Baseline | 5 / 5 | 29.825 | 246 / 0 | 6.520 | 5 | 9 | 11.65 | 3 / 72 | 8.425 | 0% | 541 / 97% |
| Normal | 20 / 20 | 30.020 | 789 / 0 | 5.304 | 5 | 7 | 9 | 3 / 64 | 26.856 | 0% | 569 / 85% |
| Higher | 50 / 50 | 30.012 | 1449 / 0 | 7.792 | 6 | 11 | 15 | 3 / 186 | 49.397 | 0% | 463 / 98% |

All 2,498 measured samples passed. Compared with the empty list, populated p95 rose from 5 to 11.65 ms at Baseline, 7 to 9 ms at Normal, and 5 to 15 ms at Higher. Throughput remained close at each matching workload and increased with user count in both datasets. Extra query work, JSON serialization, transfer, and assertion parsing could contribute to higher elapsed times. The runs do not isolate those costs. Higher briefly reached 50 threads because the ramp-up uses almost its entire scheduled duration. Peak host CPU approached 98% and sampled available memory reached 463 MiB during Higher. No extended experiments were run.

Run the same PowerShell commands from the repository root using a fresh output directory and the documented Java, JMeter and Python settings:

```powershell
$runner = '.\tests\se3112\jmeter-performance\Run-Tests.ps1'
$runRoot = Join-Path ([IO.Path]::GetTempPath()) ('eventpulse-populated-' + [Guid]::NewGuid())
pwsh -NoProfile -File $runner -Profile smoke -OutputRoot (Join-Path $runRoot 'warmup')
if ($LASTEXITCODE) { throw 'Warm-up failed' }
foreach ($profile in 'smoke','baseline','normal','higher') {
    # Verify resource headroom before continuing to the next workload.
    pwsh -NoProfile -File $runner -Profile $profile -OutputRoot (Join-Path $runRoot 'assignment')
    if ($LASTEXITCODE) { throw "Benchmark failed for $profile" }
}
```

See `populatedDataset` in [benchmark-evidence.json](benchmark-evidence.json) for timestamps, environment versions, exact run paths and hashes. The populated dashboards are under `results/phase3/assignment/<run>/html/index.html`. Open the chosen dashboard in a browser, or use `Invoke-Item` on that file in PowerShell. The dashboard's `statistics.json`, JTL, and JMeter log reproduce the table. Local `results/phase3` also contains fixture execution logs, response captures, the resource sampling CSV, and copied backend logs.

The plan measures a read-only list on a local single-instance stack. Fixture size, startup effects, and shared load-generator and application resources can affect results. A short ramp-heavy run cannot establish a steady-state capacity limit. The plan checks each response, so JSON parsing also consumes load-generator CPU.

The empty benchmark used no fixture writes. The separate seeded experiment used only the dedicated local database and preserved the original files and hashes.

## Two-minute viva

Prepare the stack and open eventpulse-events-load-test.jmx in the JMeter GUI before the timed segment. The GUI listeners are disabled for measurement. Enable View Results Tree only if needed for interactive inspection, and use the checked-in plan with the CLI runner for measured work.

```powershell
$jmeter = if ($env:JMETER_HOME) { Join-Path $env:JMETER_HOME 'bin/jmeter.bat' } else { (Get-Command jmeter.bat -ErrorAction Stop).Source }
if ($env:JAVA_HOME) { $env:Path = (Join-Path $env:JAVA_HOME 'bin') + [IO.Path]::PathSeparator + $env:Path }
$demo = Join-Path ([IO.Path]::GetTempPath()) "eventpulse-viva-$([Guid]::NewGuid())"
New-Item -ItemType Directory -Path $demo | Out-Null
Start-Process -FilePath $jmeter -WorkingDirectory $demo -ArgumentList ('-t "{0}"' -f (Resolve-Path '.\tests\se3112\jmeter-performance\eventpulse-events-load-test.jmx').Path)

& pwsh -NoProfile -File .\tests\se3112\jmeter-performance\Run-Tests.ps1 -Profile smoke -OutputRoot $demo
if ($LASTEXITCODE -eq 0) {
    $dashboard = Get-ChildItem -LiteralPath $demo -Recurse -Filter index.html | Select-Object -First 1
    Invoke-Item -LiteralPath $dashboard.FullName
}
```

Show the Thread Group, the 500 ms timer, the GET, and the three response checks. Run the 10-second smoke, open the dashboard, and explain p95 elapsed response time, requests per second, and percentage of failed samples. Rehearse on the presentation machine to account for JVM startup and dashboard generation.

| Time | Show |
|---|---|
| 0–25 s | Open the prepared JMX. Point out the single Thread Group, users, ramp-up, scheduled duration, and timer. |
| 25–45 s | Show the YARP Events GET, redirects disabled, exact 200, JSON content type, and JSON array/shape assertion. |
| 45–70 s | Run the smoke command above. Explain that it uses one thread for 10 scheduled seconds and validates the JTL after JMeter exits. |
| 70–100 s | Open `html/index.html`. Show average response time, p95, throughput, and failed-sample percentage. |
| 100–120 s | Show the CI template. Explain local readiness, zero unexpected failures, retained artifacts, and cleanup. |

The CLI rehearsal with the stack already running took 20.63 seconds including dashboard generation. It produced 15 successful samples. Time the GUI walkthrough separately.
