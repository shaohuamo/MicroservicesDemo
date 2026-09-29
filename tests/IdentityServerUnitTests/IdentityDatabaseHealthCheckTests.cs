using FluentAssertions;
using IdentityServer.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

namespace IdentityServerUnitTests;

public sealed class IdentityDatabaseHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseIsReachable_ReturnsHealthy()
    {
        var probe = new Mock<IIdentityDatabaseHealthProbe>();
        probe.Setup(x => x.CanConnectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var check = new IdentityDatabaseHealthCheck(probe.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseIsUnavailable_ReturnsUnhealthy()
    {
        var probe = new Mock<IIdentityDatabaseHealthProbe>();
        probe.Setup(x => x.CanConnectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var check = new IdentityDatabaseHealthCheck(probe.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseThrowsAndRecovers_UpdatesResult()
    {
        var probe = new Mock<IIdentityDatabaseHealthProbe>();
        probe.SetupSequence(x => x.CanConnectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"))
            .ReturnsAsync(true);
        var check = new IdentityDatabaseHealthCheck(probe.Object);

        var failed = await check.CheckHealthAsync(new HealthCheckContext());
        var recovered = await check.CheckHealthAsync(new HealthCheckContext());

        failed.Status.Should().Be(HealthStatus.Unhealthy);
        recovered.Status.Should().Be(HealthStatus.Healthy);
    }
}
