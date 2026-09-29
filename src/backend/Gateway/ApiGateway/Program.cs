using ApiGateway.ConsulServiceBuilder;
using ApiGateway.Extensions;
using ApiGateway.Middleware;
using ApiGateway.Health;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var useConsul = builder.Configuration.GetValue<bool>("UseConsul", false);

// Load ocelot config: Consul mode (Docker) uses ocelot.json stored in Consul;
// K8s mode uses a static file with hardcoded K8s Service DNS hosts
if (!useConsul)
{
    builder.Configuration.AddJsonFile("ocelot.k8s.json", optional: false, reloadOnChange: true);
}
else
{
    builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
}

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddGatewayAuthentication(builder.Configuration);

var redisConnectionString = builder.Configuration["Authentication:DenylistRedisConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var redisConfiguration = ConfigurationOptions.Parse(redisConnectionString);
        redisConfiguration.AbortOnConnectFail = false;
        redisConfiguration.ConnectRetry = 5;
        redisConfiguration.ConnectTimeout = 5000;
        redisConfiguration.SyncTimeout = 5000;

        return ConnectionMultiplexer.Connect(redisConfiguration);
    });
}

builder.AddObservability();

if (useConsul)
{
    builder.Services
        .AddOcelot(builder.Configuration)
        .AddConsul<MyConsulServiceBuilder>()
        .AddConfigStoredInConsul(); // store ocelot.json in consul server
}
else
{
    builder.Services.AddOcelot(builder.Configuration);
}

// Add health checks for Kubernetes probes
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"]);
var denylistRedisEnabled = !string.IsNullOrWhiteSpace(redisConnectionString)
    && builder.Configuration.GetValue("Authentication:DenylistFailClosed", true);
if (denylistRedisEnabled)
{
    builder.Services.AddHealthChecks()
        .AddCheck<GatewayRedisHealthCheck>("gateway_denylist_redis", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));
}

var app = builder.Build();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.UseAuthentication();
app.UseIdentityHeaders();
await app.UseOcelot();

app.Run();
