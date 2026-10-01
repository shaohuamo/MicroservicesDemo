using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ApiGateway.Extensions;

/// <summary>
/// Registers observability services for the API Gateway.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Adds OpenTelemetry logging, tracing, and metrics exporters.
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("Ocelot.ApiGateway"))
            .WithLogging(logging => logging.AddOtlpExporter(), options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
            })
            .WithTracing(tracerBuilder => tracerBuilder
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context => !IsNoisyGatewayPath(context.Request.Path);
                })
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(meterBuilder => meterBuilder
                .AddMeter("ApiGateway.Authentication")
                .AddMeter("ApiGateway.Redis")
                .AddProcessInstrumentation()
                .AddRuntimeInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .SetExemplarFilter(ExemplarFilterType.TraceBased)
                .AddOtlpExporter());

        return builder;
    }

    private static bool IsNoisyGatewayPath(PathString path) =>
        path == "/"
        || path.StartsWithSegments("/favicon.ico")
        || path.StartsWithSegments("/___proxy_subdomain_cpanel")
        || path.StartsWithSegments("/wp-json")
        || path.StartsWithSegments("/xmlrpc.php")
        || path.StartsWithSegments("/console")
        || path == "/server"
        || path.StartsWithSegments("/server-status");
}
