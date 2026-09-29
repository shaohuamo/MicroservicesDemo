using IdentityServer.Data;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer.Health;

public interface IIdentityDatabaseHealthProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}

public sealed class IdentityDatabaseHealthProbe(IServiceScopeFactory scopeFactory) : IIdentityDatabaseHealthProbe
{
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.Database.CanConnectAsync(cancellationToken);
    }
}
