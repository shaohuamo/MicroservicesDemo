using RabbitMQ.Client;

namespace CommonService.RabbitMQ;

public interface IRabbitMQConnectionProvider : IAsyncDisposable
{
    Task<IConnection> GetConnectionAsync();
}