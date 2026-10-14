# Temporal metrics via Prometheus and Grafana (local Compose)

Plan: [temporal-metrics-prometheus-grafana](../plans/temporal-metrics-prometheus-grafana.md)

## Decision

The Compose stack runs Prometheus and Grafana and scrapes two metric sources:

- **Temporal server** (`temporal:8000`), enabled with `PROMETHEUS_ENDPOINT` on the `temporalio/auto-setup` image.
- **SDK metrics** (`space-classic-worker`, `webshield-worker`, `waas-api` on port `9000`), from one Prometheus-enabled `TemporalRuntime` per process.

Grafana provisions the Prometheus datasource and two dashboards copied from
[temporalio/dashboards](https://github.com/temporalio/dashboards): `server/server-general.json`
and `sdk/temporal-core-sdks-otel.json`. The "Core SDKs" dashboard is the one that matches the
.NET SDK, which is Core-based, and it reads histograms in milliseconds, which is the
`PrometheusOptions` default (`UseSecondsForDuration = false`). Do not enable that option without
changing the dashboard.

## Constraints and trade-offs

- **One runtime per process.** The runtime owns the Prometheus listener, so a second runtime on port 9000
  fails to bind. The Webshield worker has both an injected client and a hosted worker, so both are given the
  same runtime instance. The hosted worker's runtime is set through `ConfigureOptions(o => o.ClientOptions!.Runtime = ...)`.
- **Port 9000 is hard-coded** in each `Program.cs` and `observability/prometheus.yml`. No configuration was added
  for a single local use.
- **Kubernetes:** the 9000 listener is new on the application pods and needs its own scrape
  configuration (for example a `ServiceMonitor`) when deploying. This is not done here.
- **Anonymous Grafana Admin** is for local development only and must not be reused for production.
- **Dashboards are static copies** and do not update with the upstream repository.
- **Pinned images:** `prom/prometheus:v3.15.0` and `grafana/grafana:13.2.4`.
