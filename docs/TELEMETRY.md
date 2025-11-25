# Telemetry & Observability

The solution emits OpenTelemetry traces, metrics, and logs. You can choose where to send them:

- **Local development** – point the OTLP exporter at a Grafana-compatible stack (no Aspire required).
- **Deployments** – continue using Azure Application Insights by supplying a connection string. The pipeline can export to OTLP _and_ Application Insights simultaneously.

## 1. Local Grafana Stack (OTLP HTTP/gRPC)

Use the all-in-one [Grafana OTEL LGTM](https://grafana.com/docs/grafana-cloud/monitor-infrastructure/monitor/otel-lgtm/) container. It exposes Grafana, Loki, Tempo, and Prometheus with OTLP receivers on ports `4317` (gRPC) and `4318` (HTTP).

```bash
# Start Grafana + OTLP collector + storage backends
docker run -d --name grafana-otel \
  -p 3100:3000 \   # Grafana UI (host 3100)
  -p 4317:4317 \   # OTLP gRPC (optional)
  -p 4318:4318 \   # OTLP HTTP (default in this repo)
  grafana/otel-lgtm:latest
```

Default credentials:

- **User:** `admin`
- **Password:** `admin`

Grafana already contains Tempo, Loki, and Prometheus data sources wired to the collector. No extra configuration is required unless you want to customize dashboards.

### Docker Compose alternative

A ready-to-use compose file now lives at the repo root (`docker-compose.yml`) and uses the `grafana/otel-lgtm:latest` tag with the Grafana UI bound to port 3100 on the host. Start or stop the stack with:

```bash
# Start Grafana + OTLP collector via compose
docker compose up -d grafana-otel

# Tear it down when finished
docker compose down
```

The compose service exposes the same ports as the one-off container and persists nothing else (the LGTM image already bundles Tempo/Loki/Prometheus).

## 2. Wire the apps to the local collector

Both the Web API and Console app read telemetry settings from the `Telemetry` configuration section (environment variables take precedence). To send data to the Grafana container above, keep the default endpoint (`http://localhost:4318`) or override it via environment variables:

```bash
# Example for WebApi + ConsoleApp
export Telemetry__OtlpEndpoint="http://localhost:4318"

# Run Web API
cd src/WebApi
ASPNETCORE_URLS=https://localhost:5072 dotnet run

# (Optional) Run Console app in a second terminal
cd src/ConsoleApp
Telemetery__DeploymentEnvironment=development dotnet run
```

> ℹ️ Configuration keys follow .NET's double-underscore convention (`Telemetry__ServiceName`, `Telemetry__EnableSensitiveData`, etc.).

When either app runs, the OTLP exporter streams traces/metrics/logs into Tempo/Prometheus/Loki. Open [http://localhost:3100](http://localhost:3100) → Explore to visualize traces (`Tempo`), metrics (`Prometheus`), or logs (`Loki`).

## 3. Production / Non-local environments

Set the `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable (or configure `Telemetry:ApplicationInsightsConnectionString` in `appsettings.{Environment}.json`). The telemetry pipeline automatically adds the Azure Monitor exporters **in addition to** the OTLP exporter. This allows dual streaming to Grafana (for centralized observability) and Application Insights (for Azure-native monitoring).

Example Kubernetes secret / app setting:

```bash
export APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=https://..."
```

## 4. Troubleshooting

| Symptom | Fix |
| --- | --- |
| Nothing arrives in Grafana | Verify `Telemetry:Enabled` is `true` (default), the endpoint is reachable, and ports 4318/4317 are open. |
| TLS errors | Use HTTPS endpoints only if your collector provides certificates; otherwise stick with `http://localhost:4318` for local dev. |
| Application Insights exporter not firing | Ensure the connection string is non-empty; the code only registers the Azure Monitor exporters when the string is present. |
| Excessive payload logging | Set `Telemetry:EnableSensitiveData=false` (default) to prevent prompts/responses from being recorded. |

## 5. Key Files

- `appsettings.json` / `appsettings.Development.json` in both WebApi and ConsoleApp – baseline telemetry configuration.
- `src/AgentSdk/DependencyInjection/TelemetryServiceCollectionExtensions.cs` – registers the OpenTelemetry pipeline.
- `src/AgentSdk/DependencyInjection/TelemetryLoggingBuilderExtensions.cs` – wires structured logging exporters.

With this setup you can keep using Grafana (or any OTLP-compatible backend) locally without pulling in Aspire, while production deployments can stream to Application Insights by configuration only.
