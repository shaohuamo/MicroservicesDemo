using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using NotificationsMicroservice.API.Health;
using NotificationsMicroservice.Infrastructure.Health;
using NotificationsMicroservice.Infrastructure.Persistence;
using StackExchange.Redis;

namespace NotificationsServiceUnitTests;

public sealed class NotificationHealthCheckTests
{
    #region Database

    [Fact]
    public async Task DatabaseCheck_WhenDatabaseIsReachable_ReturnsHealthy()
    {
        var probe = new Mock<INotificationDatabaseHealthProbe>();
        probe.Setup(x => x.CheckAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var check = new NotificationDatabaseHealthCheck(probe.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task DatabaseCheck_WhenDatabaseFailsAndRecovers_UpdatesResult()
    {
        var probe = new Mock<INotificationDatabaseHealthProbe>();
        probe.SetupSequence(x => x.CheckAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"))
            .Returns(Task.CompletedTask);
        var check = new NotificationDatabaseHealthCheck(probe.Object);

        var failed = await check.CheckHealthAsync(new HealthCheckContext());
        var recovered = await check.CheckHealthAsync(new HealthCheckContext());

        failed.Status.Should().Be(HealthStatus.Unhealthy);
        recovered.Status.Should().Be(HealthStatus.Healthy);
    }

    #endregion

    #region Redis

    [Fact]
    public async Task RedisCheck_WhenPingSucceeds_ReturnsHealthy()
    {
        var connection = new Mock<IConnectionMultiplexer>();
        var database = new Mock<IDatabase>();
        connection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        database.Setup(x => x.PingAsync(CommandFlags.None)).ReturnsAsync(TimeSpan.FromMilliseconds(1));
        var check = new NotificationRedisHealthCheck(connection.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task RedisCheck_WhenPingFailsAndRecovers_UpdatesResult()
    {
        var connection = new Mock<IConnectionMultiplexer>();
        var database = new Mock<IDatabase>();
        connection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        database.SetupSequence(x => x.PingAsync(CommandFlags.None))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"))
            .ReturnsAsync(TimeSpan.FromMilliseconds(1));
        var check = new NotificationRedisHealthCheck(connection.Object);

        var failed = await check.CheckHealthAsync(new HealthCheckContext());
        var recovered = await check.CheckHealthAsync(new HealthCheckContext());

        failed.Status.Should().Be(HealthStatus.Unhealthy);
        recovered.Status.Should().Be(HealthStatus.Healthy);
    }

    #endregion

    #region Consumer readiness

    [Fact]
    public async Task ConsumerCheck_WhenConsumerIsNotSubscribed_ReturnsUnhealthy()
    {
        var state = new ProductOperationConsumerHealthState();
        var check = new ProductOperationConsumerHealthCheck(state);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task ConsumerCheck_WhenConsumerDisconnectsAndRecovers_TracksOnlyConsumerState()
    {
        var state = new ProductOperationConsumerHealthState();
        var check = new ProductOperationConsumerHealthCheck(state);
        state.MarkReady();
        var connected = await check.CheckHealthAsync(new HealthCheckContext());
        state.MarkNotReady("RabbitMQ channel closed.");
        var disconnected = await check.CheckHealthAsync(new HealthCheckContext());
        state.MarkReady();
        var recovered = await check.CheckHealthAsync(new HealthCheckContext());

        connected.Status.Should().Be(HealthStatus.Healthy);
        disconnected.Status.Should().Be(HealthStatus.Unhealthy);
        recovered.Status.Should().Be(HealthStatus.Healthy);
    }

    #endregion
}
