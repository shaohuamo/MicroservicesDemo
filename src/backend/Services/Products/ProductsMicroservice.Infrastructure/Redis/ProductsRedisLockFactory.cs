using Medallion.Threading;
using Medallion.Threading.Redis;

namespace ProductsMicroservice.Infrastructure.Redis;

internal sealed class ProductsRedisLockFactory(IProductsRedisConnectionProvider connections)
    : IProductsRedisLockFactory
{
    public async Task<IDistributedLock> CreateLockAsync(string name)
    {
        var connection = await connections.GetConnectionAsync();
        if (!connection.IsConnected)
        {
            throw new InvalidOperationException("Products Redis is disconnected.");
        }

        return new RedisDistributedSynchronizationProvider(connection.GetDatabase()).CreateLock(name);
    }
}
