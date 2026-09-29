using CommonService.Middlewares;
using CommonService.RabbitMQ;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotificationsMicroservice.API.Health;
using NotificationsMicroservice.API.Extensions;
using NotificationsMicroservice.API.Middleware;
using NotificationsMicroservice.Core;
using NotificationsMicroservice.Infrastructure;
using NotificationsMicroservice.Infrastructure.Health;
using Steeltoe.Discovery.Consul;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddXmlFile(
    Path.Combine(AppContext.BaseDirectory, "Sql", "Notifications.xml"),
    optional: false,
    reloadOnChange: true);

builder.Services.AddControllers();

builder.Services.AddNotificationsMicroserviceCore();
builder.Services.AddNotificationsMicroserviceInfrastructure(builder.Configuration);

if (builder.Configuration.GetValue<bool>("UseConsul", false))
{
    builder.Services.AddConsulDiscoveryClient();
}

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<NotificationDatabaseHealthCheck>("notification_database", tags: ["ready"], timeout: TimeSpan.FromSeconds(5))
    .AddCheck<ProductOperationConsumerHealthCheck>("product_operation_consumer", tags: ["ready"])
    .AddCheck<NotificationRedisHealthCheck>("notification_redis", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));

builder.Services.AddObservability();

var app = builder.Build();

app.TraceContextMiddleware();
app.UseMiddleware<GatewayUserIdMiddleware>();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapControllers();

app.Run();
