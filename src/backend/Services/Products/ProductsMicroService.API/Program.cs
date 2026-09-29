using CommonService.Middlewares;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProductsMicroservice.Core;
using ProductsMicroservice.Infrastructure;
using ProductsMicroService.API.Extensions;
using ProductsMicroService.API.Health;
using ProductsMicroService.API.Middleware;
using ProductsMicroService.API.Security;
using ProductsMicroservice.Core.ServiceContracts;
using Steeltoe.Discovery.Consul;

// A migration Job uses the same image without starting the HTTP server or hosted services.
bool migrateOnly = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
var hostArgs = args.Where(arg => !string.Equals(arg, "--migrate", StringComparison.OrdinalIgnoreCase)).ToArray();
var builder = WebApplication.CreateBuilder(hostArgs);

//Add Observability
builder.AddObservability();

// Add services to the container.
builder.Services.AddProductsMicroserviceCore(builder.Configuration);
builder.Services.ProductsMicroserviceInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IProductOperationContextAccessor, HttpProductOperationContextAccessor>();

builder.Services.AddControllers(options =>
{
    // allow action method names to end with "Async" without removing "Async" suffix in route template
    options.SuppressAsyncSuffixInActionNames = false;
});

//Register Consul service discovery (only when deployed with Docker; K8s uses its own service discovery)
if (builder.Configuration.GetValue<bool>("UseConsul", false))
{
    builder.Services.AddConsulDiscoveryClient();
}

//Add Swagger services
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        //generate api.xml by comment of action method
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "api.xml"));
    });
}

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestProperties | HttpLoggingFields.ResponsePropertiesAndHeaders;
});

// Add health checks for Kubernetes probes
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<ProductsDatabaseHealthCheck>(
        "products_database", tags: ["ready"], timeout: TimeSpan.FromSeconds(3))
    .AddCheck<ProductsRedisHealthCheck>(
        "products_redis", tags: ["ready"], timeout: TimeSpan.FromSeconds(3));

var app = builder.Build();

if (migrateOnly)
{
    await app.MigrateDatabaseAsync();
    return;
}

// Local and Docker Compose retain startup migration; AKS runs it in a Job.
if (app.Configuration.GetValue("ProductsMigration:RunOnStartup", true))
{
    await app.MigrateDatabaseAsync();
}

// Configure the HTTP request pipeline.
app.UseExceptionHandlingMiddleware();
app.TraceContextMiddleware();

//Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => {

        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ProductsMicroService.API");
        //set swagger as root path
        options.RoutePrefix = string.Empty;
    });
}

app.UseHttpLogging();

// Map health check endpoint for Kubernetes readiness/liveness probes
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
