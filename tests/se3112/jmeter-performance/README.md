# SE3112 — Load & Performance Testing with Apache JMeter

**Module**: SE3112 Advanced Software Engineering  
**Project**: EventPulse — Event Ticket Booking Platform  
**Role**: Member 3 — Load & Performance Testing  
**Branch**: `se3112/jmeter-performance`

---

## 1. Test Objective

Measure the throughput, response time distribution, and error rate of the EventPulse public Events API under three workload profiles (Baseline, Normal, Higher Load). The test targets the YARP API Gateway to exercise the full microservice chain:

```
JMeter → YARP Gateway (:7000) → Event Service (:7102) → PostgreSQL
```

The goal is to identify latency at different concurrency levels and establish a baseline for regression detection in CI.

---

## 2. API Endpoints Tested

| # | Endpoint | Method | Auth Required | Notes |
|---|----------|--------|---------------|-------|
| 1 | `GET /api/events` | GET | No | Returns all approved/published events |
| 2 | `GET /api/events?status=published` | GET | No | Filtered read-only endpoint |

**Why these?**
- Both are public, read-only, and do not require authentication.
- They exercise the full YARP → Event Service → PostgreSQL path.
- No Stripe, payment, or write operations involved — safe for load testing.

---

## 3. Test Plan Structure

**File**: [`eventpulse-events-load-test.jmx`](./eventpulse-events-load-test.jmx)

```
TestPlan
│
├── User Defined Variables        ← protocol, host, port, users, rampup, duration, think_time_ms
├── HTTP Request Defaults         ← shared connection settings (10 s connect, 30 s response timeout)
├── Global HTTP Headers           ← Accept: application/json
│
└── Thread Group (duration-based scheduler)
    ├── Constant Timer            ← pacing delay (prevents burst — default 500 ms)
    ├── GET /api/events
    │   ├── Assert HTTP 200
    │   ├── Assert Content-Type: application/json
    │   └── Assert body starts with [ or { (not HTML)
    └── GET /api/events?status=published
        ├── Assert HTTP 200
        └── Assert Content-Type: application/json

Listeners
├── View Results Tree    (GUI use — no output in non-GUI mode)
├── Summary Report
└── Aggregate Report
```

---

## 4. Workload Profiles

| Profile | Virtual Users | Ramp-up | Duration | Think Time | Approx Requests |
|---------|--------------|---------|----------|------------|-----------------|
| **Baseline** | 5 | 10 s | 30 s | 500 ms | ~100–150 |
| **Normal** | 20 | 20 s | 30 s | 500 ms | ~400–600 |
| **Higher** | 50 | 30 s | 30 s | 300 ms | ~1 500–2 500 |

> **CI pipeline uses Baseline** (5 VU / 30 s). Normal and Higher should be run locally.  
> Higher Load should **never** be run against a production or shared evaluation environment.

---

## 5. Environment Configuration

### Local development prerequisites

| Component | Requirement |
|-----------|------------|
| Apache JMeter | ≥ 5.6.3 |
| Java | ≥ 11 (JMeter requirement) |
| .NET SDK | 10.0 |
| Docker | For Postgres + Kafka |
| Gateway port | `localhost:7000` |
| Event Service | `localhost:7102` (via Gateway) |

### Start the backend stack (PowerShell)

```powershell
# 1. Start infra
docker compose up -d postgres kafka azurite

# 2. Build
dotnet build backend/EventPulse.Backend.sln -c Release

# 3. Start services (separate terminals or use backend/dev-start.ps1)
dotnet run --project backend/gateway/src/EventPulse.Gateway --urls http://localhost:7000
dotnet run --project backend/services/event-service/src/EventPulse.EventService --urls http://localhost:7102
```

---

## 6. Local Execution

### Using the provided PowerShell script (Windows)

```powershell
cd tests/se3112/jmeter-performance

# Baseline (default)
.\Run-Tests.ps1

# Normal load
.\Run-Tests.ps1 -Profile normal

# Higher load (local only)
.\Run-Tests.ps1 -Profile higher

# Custom target
.\Run-Tests.ps1 -Profile normal -TargetHost myserver.local -TargetPort 8080
```

### Using the bash script (Linux / macOS / WSL)

```bash
cd tests/se3112/jmeter-performance

bash run-tests.sh baseline   # 5 VU
bash run-tests.sh normal     # 20 VU
bash run-tests.sh higher     # 50 VU

# Override target:
TARGET_HOST=my-server TARGET_PORT=8080 bash run-tests.sh normal
```

### Direct JMeter CLI command

```bash
# Baseline
jmeter -n \
  -t tests/se3112/jmeter-performance/eventpulse-events-load-test.jmx \
  -Jprotocol=http -Jhost=localhost -Jport=7000 \
  -Jusers=5 -Jrampup=10 -Jduration=30 -Jthink_time_ms=500 \
  -l results-baseline.jtl -e -o reports/baseline

# Normal
jmeter -n \
  -t tests/se3112/jmeter-performance/eventpulse-events-load-test.jmx \
  -Jprotocol=http -Jhost=localhost -Jport=7000 \
  -Jusers=20 -Jrampup=20 -Jduration=30 -Jthink_time_ms=500 \
  -l results-normal.jtl -e -o reports/normal

# Higher (local only — do NOT run against production)
jmeter -n \
  -t tests/se3112/jmeter-performance/eventpulse-events-load-test.jmx \
  -Jprotocol=http -Jhost=localhost -Jport=7000 \
  -Jusers=50 -Jrampup=30 -Jduration=30 -Jthink_time_ms=300 \
  -l results-higher.jtl -e -o reports/higher
```

---

## 7. CI Execution

The `jmeter-performance` job is defined in:

```
tests/se3112/jmeter-performance/jmeter-performance-job.yml
```

Hand this snippet to the **group leader** to merge into `.github/workflows/se3112-testing.yml`.

**What the CI job does:**

1. Checks out repository
2. Starts Docker infrastructure (Postgres, Kafka, Azurite)
3. Builds the backend
4. Starts Identity Service, Event Service, and YARP Gateway
5. Waits for gateway health check (`/api/events` returns HTTP 200)
6. Installs Apache JMeter 5.6.3
7. Runs **Baseline profile** (5 VU / 30 s) — safe for automated CI
8. Checks error rate < 5% (fails CI if exceeded)
9. Uploads JTL results as artifact
10. Uploads HTML dashboard as artifact

**CI command:**

```bash
jmeter -n \
  -t tests/se3112/jmeter-performance/eventpulse-events-load-test.jmx \
  -Jprotocol=http -Jhost=localhost -Jport=7000 \
  -Jusers=5 -Jrampup=10 -Jduration=30 -Jthink_time_ms=500 \
  -l TestResults/jmeter/results-baseline.jtl \
  -e -o TestResults/jmeter/reports/baseline \
  -j TestResults/jmeter/jmeter-baseline.log
```

**Expected CI runtime:** ~50–90 seconds (including JMeter download and service startup)

---

## 8. Metrics Collected

From the HTML dashboard and JTL file, record:

| Metric | Source |
|--------|--------|
| Number of virtual users | Test configuration |
| Total samples | Summary Report — `# Samples` |
| Average response time (ms) | Summary Report — `Average` |
| Median (50th pct) response time | Aggregate Report |
| 90th percentile response time | Aggregate Report — `90% Line` |
| 95th percentile response time | Aggregate Report — `95% Line` |
| Min response time | Aggregate Report |
| Max response time | Aggregate Report |
| Throughput (req/s) | Summary Report — `Throughput` |
| Error percentage | Summary Report — `Error %` |

---

## 9. Observed Results (Reference Run — Baseline-equivalent from existing JTL)

The following metrics are derived from the **existing** `results-events.jtl` found in `tests/performance-jmeter/`. This represents a 20-VU run against `localhost:7000/api/events`.

| Metric | GET /api/events | GET /api/events/{id} |
|--------|----------------|----------------------|
| Virtual users | 20 | 20 |
| Samples | ~20 | ~180 |
| Average resp. time | ~5 700 ms | ~107 ms |
| Min resp. time | ~874 ms | ~106 ms |
| Max resp. time | ~10 385 ms | ~112 ms |
| Throughput | ~1.8 req/s | ~150+ req/s |
| Error % | 0% | 0% |
| Response size | 1 461 bytes | 497 bytes |

**Observation**: The list endpoint (`/api/events`) showed high first-request latency (~10 s) suggesting cold-start or database warm-up effects. Subsequent cached requests were significantly faster. The event detail endpoint was very fast (107 ms average). All assertions passed with 0% error rate.

> **Disclaimer**: These are reference observations from an existing test run. They reflect local development conditions (no cold-start mitigation, single-machine DB + services). Do not interpret them as SLO targets.

---

## 10. Interpretation of Results

### How concurrency affects performance

- **Low concurrency (5 VU)**: Few simultaneous DB queries. Response times close to single-user baseline. Throughput is modest but stable.
- **Medium concurrency (20 VU)**: Connection pooling starts to matter. The thread queue at the DB may increase average latency. Throughput increases.
- **High concurrency (50 VU)**: The Event Service and Gateway may become resource-constrained on a dev laptop. Latency spikes are expected. Throughput may plateau or decrease (saturation point).

### What the data shows
- 0% error rate confirms the routing is correct (Gateway → Event Service → DB → JSON response, not HTML redirect).
- High first-request latency on the list endpoint is typical for cold EF Core query compilation and DB connection establishment.
- The pacing timer (500 ms / thread) ensures tests are representative, not artificially aggressive.

---

## 11. Report Locations

| What | Where |
|------|-------|
| JMX test plan | `tests/se3112/jmeter-performance/eventpulse-events-load-test.jmx` |
| Run scripts | `tests/se3112/jmeter-performance/Run-Tests.ps1` / `run-tests.sh` |
| CI job snippet | `tests/se3112/jmeter-performance/jmeter-performance-job.yml` |
| Local JTL results | `tests/se3112/jmeter-performance/results/<profile>-<timestamp>.jtl` |
| Local HTML reports | `tests/se3112/jmeter-performance/reports/<profile>-<timestamp>/index.html` |
| CI JTL artifact | GitHub Actions → Artifacts → `jmeter-jtl-results` |
| CI HTML artifact | GitHub Actions → Artifacts → `jmeter-html-report` |
| Reference JTL (old) | `tests/performance-jmeter/results-events.jtl` |

---

## 12. Limitations

1. **Local environment only** — results depend heavily on the developer machine's hardware.
2. **No SLO thresholds defined** — no agreed-upon latency budget to compare against (only error rate < 5%).
3. **Cold-start effect** — first requests incur EF Core compilation and DB connection pool creation.
4. **Single-instance topology** — all services run on one machine; contention effects differ from production.
5. **No auth load** — only public endpoints are tested; authenticated flows have additional JWT validation overhead.
6. **Docker I/O** — Postgres inside Docker on Windows may show higher latency than native Linux deployments.
7. **No concurrent write load** — GET-only tests do not reflect realistic mixed read/write workloads.

---

## 13. Viva Preparation

### 2-Minute Demo Script

1. **Show the JMX** — open in JMeter GUI, point to Thread Group, User Defined Variables, Constant Timer, assertions.
2. **Explain Thread Group** — virtual users (`users`), ramp-up (`rampup`), duration-based scheduler.
3. **Run non-GUI** — execute: `jmeter -n -t eventpulse-events-load-test.jmx -Jusers=5 -Jrampup=10 -Jduration=30 -l demo.jtl -e -o demo-report`
4. **Show HTML dashboard** — open `demo-report/index.html`, point to: Requests Summary, Statistics table, Response Time over Time graph.
5. **Interpret** — explain throughput, error %, 90th percentile response time.

### Viva Q&A Reference

| Question | Key Points |
|----------|-----------|
| **Load testing vs stress testing** | Load: test at expected/target load to measure normal performance. Stress: push beyond limits to find breaking point. |
| **Virtual users** | Simulated concurrent users, each running a thread of requests in JMeter. More VUs = more concurrent requests. |
| **Thread Groups** | JMeter element that defines how many threads (VUs), how quickly they start (ramp-up), and how long they run. |
| **Ramp-up period** | Gradual increase from 0 → N VUs over ramp-up seconds. Prevents thundering-herd cold-start spikes. |
| **Throughput** | Requests completed per second. Higher is better (up to the saturation point). |
| **Percentile response time** | 90th pct = 90% of requests completed within this time. More useful than average because average hides tail latency outliers. |
| **Error rate** | % of requests returning an unexpected response (non-200, assertion failure). Should be 0% under normal load. |
| **Performance bottlenecks** | DB connection pool exhaustion, EF Core query compilation, single-threaded Kafka consumer, garbage collection pauses. |
| **Why JMeter in CI** | Non-GUI mode (`-n`) runs headless. JTL output is machine-readable. `-e -o` generates HTML report. Thresholds can be scripted via JTL parsing. |

---

*Generated as part of SE3112 Advanced Software Engineering microservice testing pipeline.*