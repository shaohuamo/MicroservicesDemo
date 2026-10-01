using ApiGateway.ConsulServiceBuilder;
using ApiGateway.Extensions;
using ApiGateway.Middleware;
using ApiGateway.Health;
using ApiGateway.Revocation;
using Npgsql;
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

var fallbackEnabled = builder.Configuration.GetValue<bool>("Authentication:SessionFallback:Enabled");

if (fallbackEnabled)
{
    var connectionString = builder.Configuration["Authentication:SessionFallback:PostgresConnectionString"];
    var databasePassword = builder.Configuration["Authentication:SessionFallback:PostgresPassword"];
    var proofKey = builder.Configuration["Authentication:SessionFallback:ProofKeys:Current:Secret"];
    var proofId = builder.Configuration["Authentication:SessionFallback:ProofKeys:Current:Id"];
    if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databasePassword) || string.IsNullOrWhiteSpace(proofId)
        || !ValidProofKey(proofKey))
        throw new InvalidOperationException("Gateway session fallback requires PostgreSQL and a 32-byte proof key.");
    var databaseOptions = new NpgsqlConnectionStringBuilder(connectionString)
    {
        Password = databasePassword
    };
    builder.Services.AddSingleton(NpgsqlDataSource.Create(databaseOptions.ConnectionString));
    builder.Services.AddSingleton<IRefreshSessionStore, PostgresRefreshSessionStore>();
}

var redisConnectionString = builder.Configuration["Authentication:DenylistRedisConnectionString"];
if (string.IsNullOrWhiteSpace(redisConnectionString))
    throw new InvalidOperationException("Gateway denylist Redis connection string is required.");
var redisConnectionSettings = builder.Configuration
    .GetRequiredSection(DenylistRedisConnectionOptions.SectionName)
    .Get<DenylistRedisConnectionOptions>()
    ?? throw new InvalidOperationException("Gateway denylist Redis connection settings are required.");
redisConnectionSettings.Validate();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redisConfiguration = ConfigurationOptions.Parse(redisConnectionString);
    redisConfiguration.AbortOnConnectFail = redisConnectionSettings.AbortOnConnectFail;
    redisConfiguration.ConnectRetry = redisConnectionSettings.ConnectRetry;
    redisConfiguration.ConnectTimeout = redisConnectionSettings.ConnectTimeout;
    redisConfiguration.SyncTimeout = redisConnectionSettings.SyncTimeout;
    redisConfiguration.AsyncTimeout = redisConnectionSettings.AsyncTimeout;

    return ConnectionMultiplexer.Connect(redisConfiguration);
});

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
builder.Services.AddHealthChecks()
    .AddCheck<GatewayRedisHealthCheck>("gateway_auth_store", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));

var app = builder.Build();
app.UseHealthChecks("/health");
app.UseHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
});
app.UseHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.UseAuthentication();
app.Use(async (context, next) =>
{
    SessionProofHeaders.Remove(context.Request.Headers);
    if (context.Items.ContainsKey(AccessTokenRevocationValidator.UnavailableItem))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return;
    }
    await next();
});
app.UseIdentityHeaders();
await app.UseOcelot();

app.Run();

static bool ValidProofKey(string? encoded)
{
    if (string.IsNullOrWhiteSpace(encoded)) return false;
    try { return Convert.FromBase64String(encoded).Length >= 32; }
    catch (FormatException) { return false; }
}
