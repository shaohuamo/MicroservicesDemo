using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Caching;
using ProductsMicroservice.Infrastructure.Extensions;
using ProductsMicroservice.Infrastructure.Options;
using ProductsMicroservice.Infrastructure.Redis;
using ProductsMicroService.API.Health;

namespace ProductsServiceUnitTests;

public sealed class ProductsRedisRegistrationTests
{
    [Fact]
    public async Task AddProductsRedis_ConfiguredAsyncTimeout_BindsIndependentlyOfSyncTimeout()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:AsyncTimeout"] = "750",
                ["Redis:SyncTimeout"] = "5000"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProductsRedis(configuration);
        await using ServiceProvider provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RedisOptions>>().Value;

        options.AsyncTimeout.Should().Be(750);
        options.SyncTimeout.Should().Be(5000);
    }

    [Fact]
    public async Task AddProductsRedis_WhenEndpointIsUnavailable_DoesNotConnectDuringRegistrationOrResolution()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = "127.0.0.1:1",
                ["Redis:ConnectTimeout"] = "100",
                ["Redis:ConnectRetry"] = "0"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddProductsRedis(configuration);
        await using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IProductsRedisConnectionProvider>().Should().NotBeNull();
        provider.GetRequiredService<IProductsRedisLockFactory>().Should().NotBeNull();
        provider.GetRequiredService<IDistributedCache>().Should().NotBeNull();
    }

    [Fact]
    public async Task ReadsAndRedisHealthCheck_WhenRedisIsDown_FallBackToDatabaseService()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = "127.0.0.1:1",
                ["Redis:ConnectTimeout"] = "100",
                ["Redis:SyncTimeout"] = "100",
                ["Redis:ConnectRetry"] = "0",
                ["Redis:AbortOnConnectFail"] = "false"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProductsRedis(configuration);
        await using ServiceProvider provider = services.BuildServiceProvider();
        var inner = new Mock<IProductsGetterService>();
        var id = Guid.NewGuid();
        var product = new ProductResponse(id, "Database", 10, 2);
        inner.Setup(service => service.GetProductByProductIdAsync(id)).ReturnsAsync(product);
        inner.Setup(service => service.GetProductsAsync()).ReturnsAsync([product]);
        var connections = provider.GetRequiredService<IProductsRedisConnectionProvider>();
        var decorator = new ProductsGetterCachingDecorator(
            inner.Object,
            provider.GetRequiredService<IDistributedCache>(),
            connections,
            provider.GetRequiredService<IProductsRedisLockFactory>(),
            provider.GetRequiredService<IOptions<CacheOptions>>(),
            provider.GetRequiredService<ILogger<ProductsGetterCachingDecorator>>(),
            provider.GetRequiredService<IServiceScopeFactory>());

        (await decorator.GetProductByProductIdAsync(id)).Should().BeSameAs(product);
        (await decorator.GetProductsAsync()).Should().ContainSingle();
        (await new ProductsRedisHealthCheck(connections).CheckHealthAsync(new HealthCheckContext()))
            .Status.Should().Be(HealthStatus.Unhealthy);
        inner.Verify(service => service.GetProductByProductIdAsync(id), Times.Once);
        inner.Verify(service => service.GetProductsAsync(), Times.Once);
    }
}
