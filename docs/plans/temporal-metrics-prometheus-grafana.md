# Implementation Plan: temporal-metrics-prometheus-grafana

## File Impact Assessment
| File | Action | Description |
| :--- | :--- | :--- |
| `docker-compose.yml` | `Modify` | Add `PROMETHEUS_ENDPOINT` to `temporal`, plus `prometheus` and `grafana` services and their volumes. |
| `observability/prometheus.yml` | `Create` | Scrape config for the Temporal server and the three SDK processes. |
| `observability/grafana/provisioning/datasources/prometheus.yml` | `Create` | Provision Prometheus as the default Grafana datasource. |
| `observability/grafana/provisioning/dashboards/dashboards.yml` | `Create` | Provision a file-based dashboard provider. |
| `observability/grafana/dashboards/*.json` | `Create` | Temporal server and SDK dashboards taken from `temporalio/dashboards`. |
| `src/Systems/Space/Classic/WaaS.Space.Classic.Worker/Program.cs` | `Modify` | Create a Prometheus `TemporalRuntime` and set it on the hosted worker's client options. |
| `src/Systems/Webshield/WaaS.Webshield.Worker/Program.cs` | `Modify` | Create a Prometheus `TemporalRuntime` and set it on both the injected client and the hosted worker's client options. |
| `src/WaaS.WebApi/Program.cs` | `Modify` | Create a Prometheus `TemporalRuntime` and set it on the injected client. |
| `docs/decisions/temporal-metrics-prometheus-grafana.md` | `Create` | Record the metrics architecture decision and its trade-offs. |

## Walkthrough
1. Add `PROMETHEUS_ENDPOINT: 0.0.0.0:8000` to the `temporal` service so the server exposes metrics.
2. In each of the three `Program.cs` files, create one `TemporalRuntime` with `Telemetry.Metrics.Prometheus = new("0.0.0.0:9000")`. It must be a single instance per process, because two runtimes cannot bind the same port.
3. Set `options.Runtime = runtime` in the `AddTemporalClient` callbacks (Webshield worker, WebApi). Set `o.ClientOptions!.Runtime = runtime` through `ConfigureOptions` on the `AddHostedTemporalWorker` builders (Classic worker, Webshield worker).
4. Add `prometheus` and `grafana` services, named volumes, and the provisioning and scrape files under `observability/`. Reuse the `:z` mount suffix that the existing mounts use for SELinux.
5. Download the Temporal server and .NET SDK dashboards into `observability/grafana/dashboards/` and check that their datasource variable resolves to the provisioned datasource.
6. Write the decision record under `docs/decisions/`, which does not exist yet.
7. Verify with `dotnet build`, `dotnet test`, and `docker compose up -d`. Then check that every target on `http://localhost:9090/targets` is UP and that the Grafana panels show data.

## Risks and Decisions Needing Acceptance
- **Port exposure:** the metrics ports (`8000` on Temporal, `9000` on the apps) are reachable only on the Compose network, and only Prometheus (`9090`) and Grafana (`3000`) are published to the host. In Kubernetes, the `9000` metrics endpoint is a new listener on the application pods and would need its own scrape or `ServiceMonitor` configuration.
- **Grafana access:** anonymous Admin access is acceptable for local development only. This stack must not be reused for production.
- **Hard-coded port:** the `9000` bind address stays hard-coded, to avoid adding speculative configuration.
- **Third-party dashboards:** the JSON files are copied from `temporalio/dashboards` at a point in time and will not update automatically.
- **Unpinned images:** the Prometheus and Grafana images should be pinned to a specific tag rather than `latest`, to match good practice for this stack. This needs your input on versions, so I'll pick current stable tags unless you object.
