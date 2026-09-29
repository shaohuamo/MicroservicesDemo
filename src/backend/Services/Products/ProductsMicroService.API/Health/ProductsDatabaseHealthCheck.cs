using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProductsMicroservice.Infrastructure.DbContext;

namespace ProductsMicroService.API.Health;

/// <summary>Checks whether the Products database accepts connections.</summary>
/// <param name="dbContext">The Products database context.</param>
public sealed class ProductsDatabaseHealthCheck(ApplicationDbContext dbContext) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Products database is reachable.")
                : HealthCheckResult.Unhealthy("Products database is unreachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Products database check failed.", exception);
        }
    }
}
