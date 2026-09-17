# EventPulse Platform Monitoring Setup (Prometheus & Grafana)

## 1. Overview & Architecture Flow
This documentation details the telemetry and observability stack implemented for the EventPulse microservices platform under TECH-11 and TECH-12.

* **Instrumentation:** Microservices instrumented with `prometheus-net.AspNetCore` to expose metrics on `/metrics` and Azure Application Insights for distributed tracing.
* **Scraping Engine:** A containerized Prometheus instance scrapes metrics every 5 seconds from the host microservices via `host.docker.internal`.
* **Visualization Layer:** Grafana runs in Docker, connected via the internal container network (`http://prometheus:9090`) to render real-time dashboards for service health, JVM/CLR runtime stats, and RED (Rate, Errors, Duration) metrics.

[ Microservices (Host) ]  -- /metrics -->  [ Prometheus (:9090) ]  -->  [ Grafana (:3000) ]

Gateway (7000)

Identity (7101)

Event (7102)

Booking (7103)

Payment (7104)

---

## 2. Configuration Details

### Prometheus Scrape Configuration (`monitoring/prometheus.yml`)
Configured to poll internal metrics every 5 seconds across all 5 active services and the Gateway:

```yaml
global:
  scrape_interval: 5s
  evaluation_interval: 5s

scrape_configs:
  - job_name: 'prometheus'
    static_configs:
      - targets: ['localhost:9090']

  - job_name: 'eventpulse-gateway'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:7000']

  - job_name: 'eventpulse-identity'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:7101']

  - job_name: 'eventpulse-events'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:7102']

  - job_name: 'eventpulse-booking'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:7103']

  - job_name: 'eventpulse-payment'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:7104']
```

## 3. Alerts & Alert Rules
Prometheus is configured with the following active alerts located in `monitoring/alert.rules.yml`:
- **HighHttp5xxRate**: Triggers when the HTTP 5xx error rate exceeds 5% over 2 minutes (or >5 spikes in 1 min).
- **ServiceDown**: Triggers when any tracked microservice endpoint is unreachable (`up == 0`) for more than 1 minute.

You can view active alerts locally via the Prometheus UI: [http://localhost:9090/alerts](http://localhost:9090/alerts).

## 4. Simulating Failures (RED Metrics Testing)
To verify that the alerting pipeline and Grafana Dashboards are working as intended, a deliberate failure endpoint is available at `GET /api/test/fail-500` on the Identity Service.

You can trigger a rapid sequence of failures using the provided PowerShell script:
```powershell
# Send 20 rapid 500-error responses
.\scripts\simulate-failures.ps1
```
Watch the **Errors** rate climb in the Grafana dashboard (`http://localhost:3000`) and the `HighHttp5xxRate` alert transition to a FIRING state in Prometheus.

## 5. Transitioning to Azure App Services
When the microservices are deployed via CI/CD to Azure App Services, the Prometheus instance will need to point to the live HTTPS endpoints rather than `host.docker.internal`.

In `monitoring/prometheus.yml`, there is an **AZURE APP SERVICE CONFIGURATION** block. Uncomment these jobs and replace the `.azurewebsites.net` domains with the provisioned ones:
```yaml
  - job_name: 'eventpulse-azure-gateway'
    scheme: https
    metrics_path: '/metrics'
    static_configs:
      - targets: ['eventpulse-gateway-dev-xyz.azurewebsites.net:443']
```