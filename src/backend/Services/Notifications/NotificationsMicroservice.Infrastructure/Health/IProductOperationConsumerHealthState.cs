namespace NotificationsMicroservice.Infrastructure.Health;

public interface IProductOperationConsumerHealthState
{
    bool IsReady { get; }

    string? Reason { get; }

    void MarkReady();

    void MarkNotReady(string reason);
}

public sealed class ProductOperationConsumerHealthState : IProductOperationConsumerHealthState
{
    private readonly Lock _lock = new();
    private bool _isReady;
    private string? _reason = "RabbitMQ consumer has not subscribed yet.";

    public bool IsReady
    {
        get
        {
            lock (_lock)
            {
                return _isReady;
            }
        }
    }

    public string? Reason
    {
        get
        {
            lock (_lock)
            {
                return _reason;
            }
        }
    }

    public void MarkReady()
    {
        lock (_lock)
        {
            _isReady = true;
            _reason = "RabbitMQ consumer is subscribed.";
        }
    }

    public void MarkNotReady(string reason)
    {
        lock (_lock)
        {
            _isReady = false;
            _reason = reason;
        }
    }
}
