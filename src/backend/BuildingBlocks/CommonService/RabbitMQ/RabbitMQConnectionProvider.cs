using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CommonService.RabbitMQ;

public class RabbitMQConnectionProvider: IRabbitMQConnectionProvider
{
    private readonly ConnectionFactory _connectionFactory;
    private IConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly RabbitMQOptions _options;

    public RabbitMQConnectionProvider(IOptions<RabbitMQOptions> options)
    {
        _options = options.Value;

        _connectionFactory = new ConnectionFactory()
        {
            HostName = _options.HostName,
            UserName = _options.UserName,
            Password = _options.Password,
            Port = _options.Port,

            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(_options.NetworkRecoveryIntervalSeconds)
        };
    }

    //using double check locking to get a singleton connection
    public async Task<IConnection> GetConnectionAsync()
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _lock.WaitAsync();
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            _connection = await CreateConnectionWithRetryAsync();
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IConnection> CreateConnectionWithRetryAsync()
    {
        int attempt = 0;
        Exception? lastException = null;

        while (attempt < _options.MaxRetryAttempts)
        {
            try
            {
                attempt++;

                var connection = await _connectionFactory.CreateConnectionAsync();

                if (connection.IsOpen)
                    return connection;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            var delay = CalculateBackoff(attempt);
            await Task.Delay(delay);
        }

        throw new Exception(
            $"Failed to connect to RabbitMQ after {_options.MaxRetryAttempts} attempts",
            lastException);
    }

    private TimeSpan CalculateBackoff(int attempt)
    {
        var exponential = Math.Min(
            _options.InitialDelaySeconds * Math.Pow(2, attempt - 1),
            _options.MaxDelaySeconds);

        //avoid RabbitMQ overwhelmed
        var jitter = Random.Shared.NextDouble();

        return TimeSpan.FromSeconds(exponential + jitter);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            try
            {
                if (_connection.IsOpen)
                    await _connection.CloseAsync();
            }
            finally
            {
                await _connection.DisposeAsync();
            }
        }
    }
}