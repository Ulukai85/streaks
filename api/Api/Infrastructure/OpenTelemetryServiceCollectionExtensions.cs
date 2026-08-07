using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Api.Infrastructure;

public static class OpenTelemetryServiceCollectionExtensions
{
    // Exports traces, metrics, and the built-in ILogger pipeline (no separate logging library,
    // see ADR 0011) to the OTLP endpoint in OTEL_EXPORTER_OTLP_ENDPOINT - the host-level Grafana
    // Alloy collector, reached via host.docker.internal (see infrastructure/docker-compose.yml).
    public static IServiceCollection AddAppOpenTelemetry(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithLogging()
            .UseOtlpExporter();

        return services;
    }
}
