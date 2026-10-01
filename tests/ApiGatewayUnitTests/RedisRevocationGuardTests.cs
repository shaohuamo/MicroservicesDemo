using System.Net;
using ApiGateway.Revocation;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;

namespace ApiGatewayUnitTests;

public sealed class RedisRevocationGuardTests
{
    private static readonly EndPoint Endpoint = new DnsEndPoint("redis", 6379);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CheckAsync_WhenRedisHealthy_UsesDenylist(bool blacklisted)
    {
        var (guard, _, database, _) = CreateGuard();
        database.Setup(x => x.KeyExistsAsync("admin-web:access-token-denylist:jti-1", CommandFlags.None))
            .ReturnsAsync(blacklisted);

        var result = await guard.CheckAsync("jti-1");

        result.denied.Should().Be(blacklisted);
        result.fallback.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_WhenRedisCommandTimesOut_RequiresFallback()
    {
        var (guard, _, database, _) = CreateGuard();
        database.Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ThrowsAsync(new TimeoutException("Redis timed out"));

        (await guard.CheckAsync("jti-1")).Should().Be((false, true));
    }

    [Fact]
    public async Task CheckAsync_WhenConnectionCreationThrows_RequiresFallback()
    {
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IConnectionMultiplexer)))
            .Throws(new InvalidOperationException("Redis connection unavailable"));
        var guard = new RedisRevocationGuard(services.Object, new ConfigurationBuilder().Build(), TimeProvider.System);

        (await guard.CheckAsync("jti-1")).Should().Be((false, true));
    }

    private static (RedisRevocationGuard guard, Mock<IConnectionMultiplexer> redis,
        Mock<IDatabase> database, Mock<IServer> server) CreateGuard()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        var database = new Mock<IDatabase>();
        var server = new Mock<IServer>();
        redis.SetupGet(x => x.IsConnected).Returns(true);
        redis.Setup(x => x.GetEndPoints(It.IsAny<bool>())).Returns([Endpoint]);
        redis.Setup(x => x.GetServer(Endpoint, It.IsAny<object>())).Returns(server.Object);
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        server.Setup(x => x.ExecuteAsync("INFO", It.IsAny<object[]>())).ReturnsAsync(
            RedisResult.Create((RedisValue)"run_id:healthy\nuptime_in_seconds:5000\nevicted_keys:0\n"));
        var services = new ServiceCollection().AddSingleton(redis.Object).BuildServiceProvider();
        return (new RedisRevocationGuard(services,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:AccessTokenDenylistPrefix"] = "admin-web:access-token-denylist"
            }).Build(), TimeProvider.System), redis, database, server);
    }
}
