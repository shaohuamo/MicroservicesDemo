namespace TestMicroservice.API.RabbitMQ
{
    public interface IRabbitMQProductAddConsumer:IAsyncDisposable
    {
        Task ConsumeAsync();
    }
}
