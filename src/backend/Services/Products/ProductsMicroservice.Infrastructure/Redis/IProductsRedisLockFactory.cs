using Medallion.Threading;

namespace ProductsMicroservice.Infrastructure.Redis;

public interface IProductsRedisLockFactory
{
    Task<IDistributedLock> CreateLockAsync(string name);
}
