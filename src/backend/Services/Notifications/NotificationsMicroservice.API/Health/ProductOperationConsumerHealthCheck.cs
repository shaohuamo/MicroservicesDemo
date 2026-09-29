using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotificationsMicroservice.Infrastructure.Health;

namespace NotificationsMicroservice.API.Health;

public sealed class ProductOperationConsumerHealthCheck(IProductOperationConsumerHealthState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = state.IsReady
            ? HealthCheckResult.Healthy(state.Reason)
            : HealthCheckResult.Unhealthy(state.Reason ?? "RabbitMQ consumer is not ready.");

        return Task.FromResult(result);
    }
}
