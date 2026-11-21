using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Atlas.ServiceDefaults.Observability;

/// <summary>
/// OpenTelemetry wiring for traces, metrics and logs.
/// </summary>
/// <remarks>
/// We emit OTLP and nothing vendor-specific. The collector decides where the
/// data lands (Grafana/Tempo, Honeycomb, Datadog, Azure Monitor...), so
/// swapping observability vendors is an infrastructure change, not a code
/// change. Standard <c>OTEL_*</c> environment variables are honoured, which is
/// what every hosting platform and sidecar already sets.
/// </remarks>
public static class ObservabilityExtensions
{
    /// <summary>Activity source name for hand-rolled spans in application code.</summary>
    public const string ActivitySourceName = "Atlas";

    /// <summary>Meter name for hand-rolled metrics in application code.</summary>
    public const string MeterName = "Atlas";

    /// <summary>
    /// Configures structured logging, metrics and distributed tracing.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            // Structured logs correlated with traces by trace/span id.
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: builder.Environment.ApplicationName,
                    serviceVersion: typeof(ObservabilityExtensions).Assembly
                        .GetName().Version?.ToString() ?? "0.0.0",
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes([
                    new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName),
                ]))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(MeterName))
            .WithTracing(tracing => tracing
                .AddSource(ActivitySourceName)
                .AddAspNetCoreInstrumentation(options =>
                    // Health and metrics probes would otherwise dominate the trace
                    // volume and tell us nothing.
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
                        && !context.Request.Path.StartsWithSegments("/alive", StringComparison.OrdinalIgnoreCase))
                .AddHttpClientInstrumentation());

        // Only attach the exporter when a collector endpoint is configured, so
        // local runs without a collector stay silent instead of logging
        // connection failures every few seconds.
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
}
