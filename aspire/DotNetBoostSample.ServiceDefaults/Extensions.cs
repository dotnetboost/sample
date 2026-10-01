using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Cross-cutting defaults shared by every service the AppHost starts: OpenTelemetry
/// (logs, metrics, traces exported over OTLP to the Aspire dashboard), health checks,
/// service discovery, and resilient <see cref="HttpClient"/> defaults.
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath    = "/health";
    private const string AlivenessEndpointPath = "/alive";

    /// <summary>Applies the shared telemetry, health-check and HTTP defaults.</summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Retries, circuit breaker and timeouts for every outbound HttpClient.
            http.AddStandardResilienceHandler();

            // Lets HttpClient resolve "https+http://api" style addresses to real endpoints.
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>Configures OpenTelemetry logging, metrics and tracing with the OTLP exporter.</summary>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes           = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddAspNetCoreInstrumentation(tracing =>
                    // The health endpoints are polled constantly; tracing them is pure noise.
                    tracing.Filter = context =>
                        !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                        && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                .AddHttpClientInstrumentation());

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // The AppHost injects OTEL_EXPORTER_OTLP_ENDPOINT so telemetry lands in the dashboard.
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

        return builder;
    }

    /// <summary>Registers the liveness check that <see cref="MapDefaultEndpoints"/> exposes.</summary>
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHealthChecks()
            // Every registered check must pass for the app to be considered ready.
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps <c>/health</c> (ready) and <c>/alive</c> (live). Development only — these
    /// endpoints expose dependency state, so put them behind auth before enabling elsewhere.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
            return app;

        // All health checks must pass for the app to accept traffic.
        app.MapHealthChecks(HealthEndpointPath);

        // Only checks tagged "live" decide whether the app is alive at all.
        app.MapHealthChecks(AlivenessEndpointPath, new()
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }
}
