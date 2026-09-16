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