using ApiGateway.Health;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using ApiGateway.Revocation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace ApiGatewayUnitTests;

public sealed class GatewayRedisHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenRedisResponds_ReturnsHealthy()
    {
        var connection = new Mock<IConnectionMultiplexer>();
        var database = new Mock<IDatabase>();
        connection.SetupGet(x => x.IsConnected).Returns(true);
        connection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        database.Setup(x => x.PingAsync(CommandFlags.None)).ReturnsAsync(TimeSpan.FromMilliseconds(1));
        var check = new GatewayRedisHealthCheck(Services(connection.Object));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenRedisFailsAndRecovers_UpdatesResult()
    {
        var connection = new Mock<IConnectionMultiplexer>();
        var database = new Mock<IDatabase>();
        connection.SetupGet(x => x.IsConnected).Returns(true);
        connection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        database.SetupSequence(x => x.PingAsync(CommandFlags.None))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"))
            .ReturnsAsync(TimeSpan.FromMilliseconds(1));
        var check = new GatewayRedisHealthCheck(Services(connection.Object));

        var failed = await check.CheckHealthAsync(new HealthCheckContext());
        var recovered = await check.CheckHealthAsync(new HealthCheckContext());

        failed.Status.Should().Be(HealthStatus.Unhealthy);
        recovered.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenRedisFailsAndSessionDatabaseIsAvailable_ReturnsHealthy()
    {
        var connection = new Mock<IConnectionMultiplexer>();
        var store = new Mock<IRefreshSessionStore>();
        store.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:SessionFallback:Enabled"] = "true",
            ["Authentication:SessionFallback:ProofKeys:Current:Id"] = "v1",
            ["Authentication:SessionFallback:ProofKeys:Current:Secret"] = Convert.ToBase64String(new byte[32])
        }).Build();
        var services = new ServiceCollection()
            .AddSingleton(connection.Object)
            .AddSingleton(new SessionProofVerifier(config, TimeProvider.System))
            .AddSingleton(store.Object)
            .BuildServiceProvider();

        var result = await new GatewayRedisHealthCheck(services).CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenBothStoresFail_ReturnsUnhealthy()
    {
        var connection = new Mock<IConnectionMultiplexer>();
        var database = new Mock<IDatabase>();
        connection.SetupGet(x => x.IsConnected).Returns(true);
        connection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        database.Setup(x => x.PingAsync(CommandFlags.None)).ThrowsAsync(new TimeoutException());
        var store = new Mock<IRefreshSessionStore>();
        store.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:SessionFallback:Enabled"] = "true",
            ["Authentication:SessionFallback:ProofKeys:Current:Id"] = "v1",
            ["Authentication:SessionFallback:ProofKeys:Current:Secret"] = Convert.ToBase64String(new byte[32])
        }).Build();
        var services = new ServiceCollection()
            .AddSingleton(connection.Object)
            .AddSingleton(new SessionProofVerifier(config, TimeProvider.System))
            .AddSingleton(store.Object)
            .BuildServiceProvider();

        var result = await new GatewayRedisHealthCheck(services).CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    private static IServiceProvider Services(IConnectionMultiplexer connection) => new ServiceCollection()
        .AddSingleton(connection)
        .AddSingleton(new SessionProofVerifier(new ConfigurationBuilder().Build(), TimeProvider.System))
        .BuildServiceProvider();
}
