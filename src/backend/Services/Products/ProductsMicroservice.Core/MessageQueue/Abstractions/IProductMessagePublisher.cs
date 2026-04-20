namespace ProductsMicroservice.Core.MessageQueue.Abstractions;

public interface IProductMessagePublisher
{
    Task PublishAsync<T>(string routingKey, T message);
}