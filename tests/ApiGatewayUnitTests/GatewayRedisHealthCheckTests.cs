using ApiGateway.Health;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using StackExchange.Redis;

namespace ApiGatewayUnitTests;

public sealed class GatewayRedisHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenRedisResponds_ReturnsHealthy()
    {
        var connection = new Mock<IConnectionMultiplexer>();
        var database = new Mock<IDatabase>();
        connection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        database.Setup(x => x.PingAsync(CommandFlags.None)).ReturnsAsync(TimeSpan.FromMilliseconds(1));
        var check = new GatewayRedisHealthCheck(connection.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenRedisFailsAndRecovers_UpdatesResult()
    {
        var connection = new Mock<IConnectionMultiplexer>();
        var database = new Mock<IDatabase>();
        connection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        database.SetupSequence(x => x.PingAsync(CommandFlags.None))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"))
            .ReturnsAsync(TimeSpan.FromMilliseconds(1));
        var check = new GatewayRedisHealthCheck(connection.Object);

        var failed = await check.CheckHealthAsync(new HealthCheckContext());
        var recovered = await check.CheckHealthAsync(new HealthCheckContext());

        failed.Status.Should().Be(HealthStatus.Unhealthy);
        recovered.Status.Should().Be(HealthStatus.Healthy);
    }
}
