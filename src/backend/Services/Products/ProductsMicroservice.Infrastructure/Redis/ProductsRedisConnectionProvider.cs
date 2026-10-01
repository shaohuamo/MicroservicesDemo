using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductsMicroservice.Infrastructure.Options;
using StackExchange.Redis;

namespace ProductsMicroservice.Infrastructure.Redis;

internal sealed class ProductsRedisConnectionProvider : IProductsRedisConnectionProvider, IDisposable, IAsyncDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private readonly ConfigurationOptions _configuration;
    private readonly ILogger<ProductsRedisConnectionProvider> _logger;
    private readonly Func<ConfigurationOptions, Task<IConnectionMultiplexer>> _connect;
    private readonly Func<DateTimeOffset> _now;
    private readonly object _gate = new();
    private Task<IConnectionMultiplexer>? _connectionTask;
    private IConnectionMultiplexer? _connection;
    private DateTimeOffset _retryAfter;
    private Action<IConnectionMultiplexer>? _observers;
    private bool _disposed;

    public ProductsRedisConnectionProvider(
        IOptions<RedisOptions> options,
        ILogger<ProductsRedisConnectionProvider> logger)
        : this(options, logger,
            async configuration => await ConnectionMultiplexer.ConnectAsync(configuration),
            () => DateTimeOffset.UtcNow)
    {
    }

    internal ProductsRedisConnectionProvider(
        IOptions<RedisOptions> options,
        ILogger<ProductsRedisConnectionProvider> logger,
        Func<ConfigurationOptions, Task<IConnectionMultiplexer>> connect,
        Func<DateTimeOffset> now)
    {
        _logger = logger;
        _connect = connect;
        _now = now;
        RedisOptions settings = options.Value;
        _configuration = ConfigurationOptions.Parse(settings.ConnectionString);
        _configuration.ConnectRetry = settings.ConnectRetry;
        _configuration.ConnectTimeout = settings.ConnectTimeout;
        _configuration.SyncTimeout = settings.SyncTimeout;
        _configuration.AsyncTimeout = settings.AsyncTimeout;
        _configuration.AbortOnConnectFail = settings.AbortOnConnectFail;
        _configuration.BacklogPolicy = BacklogPolicy.FailFast;
        _configuration.ReconnectRetryPolicy = new ExponentialRetry(
            settings.InitialReconnectDelay, settings.MaxReconnectDelay);
    }

    public Task<IConnectionMultiplexer> GetConnectionAsync()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_connectionTask is not null &&
                (!_connectionTask.IsFaulted && !_connectionTask.IsCanceled ||
                 _now() < _retryAfter))
            {
                return _connectionTask;
            }

            _connectionTask = ConnectAsync();
            return _connectionTask;
        }
    }

    public void RegisterConnectionObserver(Action<IConnectionMultiplexer> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        IConnectionMultiplexer? existing;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _observers += observer;
            existing = _connection;
        }

        if (existing is not null)
        {
            NotifyObserver(observer, existing);
        }
    }

    private async Task<IConnectionMultiplexer> ConnectAsync()
    {
        try
        {
            IConnectionMultiplexer connection = await _connect(_configuration);
            Action<IConnectionMultiplexer>? observers;
            lock (_gate)
            {
                if (_disposed)
                {
                    connection.Dispose();
                    throw new ObjectDisposedException(nameof(ProductsRedisConnectionProvider));
                }
                _connection = connection;
                observers = _observers;
            }

            if (observers is not null)
            {
                foreach (Action<IConnectionMultiplexer> observer in observers.GetInvocationList())
                {
                    NotifyObserver(observer, connection);
                }
            }

            return connection;
        }
        catch
        {
            lock (_gate)
            {
                _retryAfter = _now() + RetryDelay;
            }
            throw;
        }
    }

    private void NotifyObserver(Action<IConnectionMultiplexer> observer, IConnectionMultiplexer connection)
    {
        try
        {
            observer(connection);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis connection observer failed");
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task<IConnectionMultiplexer>? task;
        lock (_gate)
        {
            _disposed = true;
            task = _connectionTask;
        }

        if (task is not null)
        {
            try
            {
                (await task).Dispose();
            }
            catch
            {
                // A failed initial connection has no multiplexer to dispose.
            }
        }
    }

    public void Dispose()
    {
        IConnectionMultiplexer? connection;
        lock (_gate)
        {
            _disposed = true;
            connection = _connection;
        }
        connection?.Dispose();
    }
}
