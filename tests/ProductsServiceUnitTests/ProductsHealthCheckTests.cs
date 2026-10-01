using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using ProductsMicroservice.Infrastructure.DbContext;
using ProductsMicroService.API.Health;
using StackExchange.Redis;
using ProductsMicroservice.Infrastructure.Redis;

namespace ProductsServiceUnitTests;

public sealed class ProductsHealthCheckTests
{
    private readonly Mock<IConnectionMultiplexer> _connection = new();
    private readonly Mock<IProductsRedisConnectionProvider> _connections = new();
    private readonly Mock<IDatabase> _redisDatabase = new();

    public ProductsHealthCheckTests()
    {
        _connection.Setup(connection => connection.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_redisDatabase.Object);
        _connections.Setup(provider => provider.GetConnectionAsync()).ReturnsAsync(_connection.Object);
    }

    #region Database

    [Fact]
    public async Task DatabaseCheck_WhenConnectionSucceeds_ReturnsHealthy()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        await using ApplicationDbContext dbContext = database.CreateContext();
        var check = new ProductsDatabaseHealthCheck(dbContext);

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task DatabaseCheck_WhenConnectionFails_ReturnsUnhealthy()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"missing-products-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(new SqliteConnection($"Data Source={missingPath};Mode=ReadOnly"))
            .Options;
        await using var dbContext = new ApplicationDbContext(options);
        var check = new ProductsDatabaseHealthCheck(dbContext);

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task DatabaseCheck_WhenDatabaseBecomesAvailable_Recovers()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"products-health-{Guid.NewGuid():N}.db");
        try
        {
            await using var connection = new SqliteConnection(
                $"Data Source={databasePath};Mode=ReadWrite;Pooling=False");
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var dbContext = new ApplicationDbContext(options);
            var check = new ProductsDatabaseHealthCheck(dbContext);

            HealthCheckResult failed = await check.CheckHealthAsync(new HealthCheckContext());
            await using (var creator = new SqliteConnection(
                $"Data Source={databasePath};Mode=ReadWriteCreate;Pooling=False"))
            {
                await creator.OpenAsync();
            }
            HealthCheckResult recovered = await check.CheckHealthAsync(new HealthCheckContext());

            failed.Status.Should().Be(HealthStatus.Unhealthy);
            recovered.Status.Should().Be(HealthStatus.Healthy);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    #endregion

    #region Redis

    [Fact]
    public async Task RedisCheck_WhenPingSucceeds_ReturnsHealthy()
    {
        _redisDatabase.Setup(database => database.PingAsync(CommandFlags.None))
            .ReturnsAsync(TimeSpan.FromMilliseconds(1));
        var check = new ProductsRedisHealthCheck(_connections.Object);

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        _redisDatabase.Verify(database => database.PingAsync(CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task RedisCheck_WhenPingFailsAndLaterRecovers_UpdatesResult()
    {
        _redisDatabase.SetupSequence(database => database.PingAsync(CommandFlags.None))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"))
            .ReturnsAsync(TimeSpan.FromMilliseconds(1));
        var check = new ProductsRedisHealthCheck(_connections.Object);

        HealthCheckResult failed = await check.CheckHealthAsync(new HealthCheckContext());
        HealthCheckResult recovered = await check.CheckHealthAsync(new HealthCheckContext());

        failed.Status.Should().Be(HealthStatus.Unhealthy);
        recovered.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task RedisCheck_WhenCancelled_ReturnsUnhealthy()
    {
        var pendingPing = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        _redisDatabase.Setup(database => database.PingAsync(CommandFlags.None))
            .Returns(pendingPing.Task);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var check = new ProductsRedisHealthCheck(_connections.Object);

        HealthCheckResult result = await check.CheckHealthAsync(
            new HealthCheckContext(), cancellation.Token);

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task RedisCheck_WhenInitialConnectionFails_ReturnsUnhealthy()
    {
        _connections.Setup(provider => provider.GetConnectionAsync())
            .ThrowsAsync(new InvalidOperationException("Redis offline"));
        var check = new ProductsRedisHealthCheck(_connections.Object);

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    #endregion
}
