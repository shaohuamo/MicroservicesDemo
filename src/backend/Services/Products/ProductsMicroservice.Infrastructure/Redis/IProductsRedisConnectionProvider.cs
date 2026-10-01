using StackExchange.Redis;

namespace ProductsMicroservice.Infrastructure.Redis;

public interface IProductsRedisConnectionProvider
{
    Task<IConnectionMultiplexer> GetConnectionAsync();

    void RegisterConnectionObserver(Action<IConnectionMultiplexer> observer);
}
