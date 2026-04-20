namespace TestMicroservice.API.RabbitMQ
{
    public class RabbitMQProductAddHostedService : IHostedService
    {
        private readonly IRabbitMQProductAddConsumer _productAddConsumer;

        public RabbitMQProductAddHostedService(IRabbitMQProductAddConsumer productAddConsumer)
        {
            _productAddConsumer = productAddConsumer;
        }

        //will be invoked when app start
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _productAddConsumer.ConsumeAsync();
        }

        //will be invoked when app stop
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _productAddConsumer.DisposeAsync();
        }
    }
}
