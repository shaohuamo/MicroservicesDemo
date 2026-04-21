using CommonService.RabbitMQ;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ProductsMicroservice.Core.Diagnostics;
using ProductsMicroservice.Infrastructure.HostedServices;

namespace ProductsMicroService.API.Extensions
{
    /// <summary>
    /// ObservabilityExtensions
    /// </summary>
    public static class ObservabilityExtensions
    {
        /// <summary>
        /// Add Telemetry with OpenTelemetry
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                .AddService(DiagnosticsConfig.ServiceName))

                //Logging
                .WithLogging(logging =>logging.AddOtlpExporter(), options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                })

                //Trace
                .WithTracing(tracerBuilder => tracerBuilder
                    .AddSource(DiagnosticsConfig.ServiceName)
                    .AddSource(RabbitMQTelemetry.ActivitySource.Name)//activity source for RabbitMQ instrumentation
                    .AddSource(AppWarmupService.ServiceName)
                    .AddRedisInstrumentation(options =>
                    {
                        options.SetVerboseDatabaseStatements = true;
                        options.Filter = (ctx) =>
                        !ctx.ToString().Contains("CLIENT", StringComparison.OrdinalIgnoreCase);
                    })
                    .AddNpgsql()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.FilterHttpRequestMessage = (req) =>
                            req.RequestUri is null ||
                            !req.RequestUri.Host.Contains("consul", StringComparison.OrdinalIgnoreCase);
                    })
                    .AddOtlpExporter())

                //Metric
                .WithMetrics(meterBuilder =>
                    meterBuilder
                    //custom metric
                    .AddMeter(DiagnosticsConfig.ProductMeter.Name)
                    .AddMeter(AppWarmupService.WarmupMeter.Name)
                    .AddProcessInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddNpgsqlInstrumentation()
                    .SetExemplarFilter(ExemplarFilterType.TraceBased)
                    .AddOtlpExporter());

            return builder;
        }
    }
}