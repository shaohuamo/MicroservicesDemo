using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProductsMicroservice.Infrastructure.Options;
using ProductsMicroservice.Infrastructure.Redis;
using StackExchange.Redis;

namespace ProductsServiceUnitTests;

public sealed class ProductsRedisConnectionProviderTests
{
    private readonly Mock<IConnectionMultiplexer> _connection = new();
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task GetConnectionAsync_DefaultOptions_UsesTwoSecondAsyncTimeoutAndFailFast()
    {
        ConfigurationOptions? captured = null;
        await using var provider = CreateProvider(configuration =>
        {
            captured = configuration;
            return Task.FromResult(_connection.Object);
        });

        await provider.GetConnectionAsync();

        captured.Should().NotBeNull();
        captured!.AsyncTimeout.Should().Be(2000);
        captured.SyncTimeout.Should().Be(5000);
        captured.BacklogPolicy.Should().BeSameAs(BacklogPolicy.FailFast);
    }

    [Fact]
    public async Task GetConnectionAsync_ConfiguredTimeouts_PreservesIndependentValues()
    {
        ConfigurationOptions? captured = null;
        await using var provider = new ProductsRedisConnectionProvider(
            Options.Create(new RedisOptions { SyncTimeout = 5000, AsyncTimeout = 750 }),
            NullLogger<ProductsRedisConnectionProvider>.Instance,
            configuration =>
            {
                captured = configuration;
                return Task.FromResult(_connection.Object);
            },
            () => _now);

        await provider.GetConnectionAsync();

        captured!.AsyncTimeout.Should().Be(750);
        captured.SyncTimeout.Should().Be(5000);
    }

    [Fact]
    public async Task GetConnectionAsync_FirstUseConnectsOnceForConcurrentCallers()
    {
        var pending = new TaskCompletionSource<IConnectionMultiplexer>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int attempts = 0;
        await using var provider = CreateProvider(_ =>
        {
            attempts++;
            return pending.Task;
        });
        attempts.Should().Be(0);

        Task<IConnectionMultiplexer> first = provider.GetConnectionAsync();
        Task<IConnectionMultiplexer> second = provider.GetConnectionAsync();
        pending.SetResult(_connection.Object);

        (await first).Should().BeSameAs(_connection.Object);
        (await second).Should().BeSameAs(_connection.Object);
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task GetConnectionAsync_FailedConnectRetriesAfterFiveSeconds()
    {
        int attempts = 0;
        await using var provider = CreateProvider(_ =>
        {
            attempts++;
            return attempts == 1
                ? Task.FromException<IConnectionMultiplexer>(new InvalidOperationException("Redis offline"))
                : Task.FromResult(_connection.Object);
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetConnectionAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetConnectionAsync());
        attempts.Should().Be(1);

        _now += TimeSpan.FromSeconds(5);
        (await provider.GetConnectionAsync()).Should().BeSameAs(_connection.Object);
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task RegisterConnectionObserver_ConnectBeforeOrAfterRegistration_NotifiesOnce()
    {
        await using var provider = CreateProvider(_ => Task.FromResult(_connection.Object));
        int before = 0;
        int after = 0;
        provider.RegisterConnectionObserver(_ => before++);

        await provider.GetConnectionAsync();
        provider.RegisterConnectionObserver(_ => after++);
        await provider.GetConnectionAsync();

        before.Should().Be(1);
        after.Should().Be(1);
    }

    private ProductsRedisConnectionProvider CreateProvider(
        Func<ConfigurationOptions, Task<IConnectionMultiplexer>> connect) =>
        new(Options.Create(new RedisOptions()),
            NullLogger<ProductsRedisConnectionProvider>.Instance, connect, () => _now);
}
