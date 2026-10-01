using System.Text;
using System.Text.Json;
using FluentAssertions;
using Medallion.Threading;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Caching;
using ProductsMicroservice.Infrastructure.Options;
using ProductsMicroservice.Infrastructure.Redis;
using StackExchange.Redis;

namespace ProductsMicroservice.Tests;

public class ProductsGetterCachingDecoratorTests
{
    private readonly Mock<IProductsGetterService> _innerMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<IProductsRedisConnectionProvider> _connectionsMock = new();
    private readonly Mock<IConnectionMultiplexer> _connectionMock = new();
    private readonly Mock<IProductsRedisLockFactory> _lockFactoryMock = new();
    private readonly Mock<ILogger<ProductsGetterCachingDecorator>> _loggerMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly CacheOptions _options = new()
    {
        DefaultExpirationMinutes = 30,
        NegativeCacheExpirationMinutes = 2,
        NullValuePlaceholder = "missing"
    };

    public ProductsGetterCachingDecoratorTests()
    {
        _connectionMock.SetupGet(connection => connection.IsConnected).Returns(true);
        _connectionsMock.Setup(connections => connections.GetConnectionAsync())
            .ReturnsAsync(_connectionMock.Object);
    }

    private ProductsGetterCachingDecorator CreateDecorator()
    {
        return new ProductsGetterCachingDecorator(
            _innerMock.Object,
            _cacheMock.Object,
            _connectionsMock.Object,
            _lockFactoryMock.Object,
            Options.Create(_options),
            _loggerMock.Object,
            _scopeFactoryMock.Object);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldReturnCachedProduct_WithoutCallingInnerService()
    {
        var productId = Guid.NewGuid();
        var cached = new ProductResponse(productId, "Cached", 12, 3);
        SetupCachedString(ProductCacheKeys.GetDetailsKey(productId), JsonSerializer.Serialize(cached));

        var result = await CreateDecorator().GetProductByProductIdAsync(productId);

        result.Should().BeEquivalentTo(cached);
        _innerMock.Verify(x => x.GetProductByProductIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldReturnNull_ForNegativeCacheHit()
    {
        var productId = Guid.NewGuid();
        SetupCachedString(ProductCacheKeys.GetDetailsKey(productId), _options.NullValuePlaceholder);

        var result = await CreateDecorator().GetProductByProductIdAsync(productId);

        result.Should().BeNull();
        _innerMock.Verify(x => x.GetProductByProductIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldCacheAndReturnProduct_OnCacheMiss()
    {
        var productId = Guid.NewGuid();
        var response = new ProductResponse(productId, "Database", 20, 4);
        SetupCachedString(ProductCacheKeys.GetDetailsKey(productId), null);
        _innerMock.Setup(x => x.GetProductByProductIdAsync(productId)).ReturnsAsync(response);

        var result = await CreateDecorator().GetProductByProductIdAsync(productId);

        result.Should().BeSameAs(response);
        _cacheMock.Verify(x => x.SetAsync(
            ProductCacheKeys.GetDetailsKey(productId),
            It.Is<byte[]>(value => Encoding.UTF8.GetString(value).Contains("Database")),
            It.Is<DistributedCacheEntryOptions>(options =>
                options.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(_options.DefaultExpirationMinutes)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldSetNegativeCache_WhenProductIsMissing()
    {
        var productId = Guid.NewGuid();
        SetupCachedString(ProductCacheKeys.GetDetailsKey(productId), null);
        _innerMock.Setup(x => x.GetProductByProductIdAsync(productId))
            .ReturnsAsync((ProductResponse?)null);

        var result = await CreateDecorator().GetProductByProductIdAsync(productId);

        result.Should().BeNull();
        _cacheMock.Verify(x => x.SetAsync(
            ProductCacheKeys.GetDetailsKey(productId),
            It.Is<byte[]>(value => Encoding.UTF8.GetString(value) == _options.NullValuePlaceholder),
            It.Is<DistributedCacheEntryOptions>(options =>
                options.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(_options.NegativeCacheExpirationMinutes)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldReturnFreshCachedProducts_WithoutCallingInnerService()
    {
        List<ProductResponse> products = [new ProductResponse(Guid.NewGuid(), "Cached", 15, 2)];
        var wrapper = new RedisDataWrapper<List<ProductResponse>>
        {
            Data = products,
            LogicExpireTime = DateTime.Now.AddMinutes(5)
        };
        SetupCachedString(ProductCacheKeys.AllProductsKey, JsonSerializer.Serialize(wrapper));

        var result = await CreateDecorator().GetProductsAsync();

        result.Should().BeEquivalentTo(products);
        _innerMock.Verify(x => x.GetProductsAsync(), Times.Never);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_WhenRedisReadFails_ReturnsDatabaseValueWithoutFillingCache()
    {
        var id = Guid.NewGuid();
        var product = new ProductResponse(id, "Database", 20, 4);
        _cacheMock.Setup(cache => cache.GetAsync(ProductCacheKeys.GetDetailsKey(id), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis offline"));
        _innerMock.Setup(inner => inner.GetProductByProductIdAsync(id)).ReturnsAsync(product);

        var result = await CreateDecorator().GetProductByProductIdAsync(id);

        result.Should().BeSameAs(product);
        _innerMock.Verify(inner => inner.GetProductByProductIdAsync(id), Times.Once);
        _cacheMock.Verify(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_WhenRedisIsDisconnected_SkipsCacheAndReadsDatabase()
    {
        var id = Guid.NewGuid();
        _connectionMock.SetupGet(connection => connection.IsConnected).Returns(false);
        _innerMock.Setup(inner => inner.GetProductByProductIdAsync(id))
            .ReturnsAsync((ProductResponse?)null);

        var result = await CreateDecorator().GetProductByProductIdAsync(id);

        result.Should().BeNull();
        _cacheMock.Verify(cache => cache.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetProductByProductIdAsync_WhenCacheFillFails_PreservesDatabaseResult(bool exists)
    {
        var id = Guid.NewGuid();
        var product = new ProductResponse(id, "Database", 20, 4);
        SetupCachedString(ProductCacheKeys.GetDetailsKey(id), null);
        _innerMock.Setup(inner => inner.GetProductByProductIdAsync(id))
            .ReturnsAsync(exists ? product : null);
        _cacheMock.Setup(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis offline"));

        var result = await CreateDecorator().GetProductByProductIdAsync(id);

        result.Should().Be(exists ? product : null);
        _innerMock.Verify(inner => inner.GetProductByProductIdAsync(id), Times.Once);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_WhenDatabaseFails_DoesNotHideDatabaseException()
    {
        var id = Guid.NewGuid();
        _connectionsMock.Setup(connections => connections.GetConnectionAsync())
            .ThrowsAsync(new InvalidOperationException("Redis offline"));
        var databaseError = new InvalidOperationException("Database offline");
        _innerMock.Setup(inner => inner.GetProductByProductIdAsync(id)).ThrowsAsync(databaseError);

        Func<Task> act = () => CreateDecorator().GetProductByProductIdAsync(id);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(databaseError);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_WhenRedisRecovers_UsesCacheAgain()
    {
        var id = Guid.NewGuid();
        var databaseProduct = new ProductResponse(id, "Database", 20, 4);
        var cachedProduct = new ProductResponse(id, "Cached", 20, 4);
        _connectionMock.SetupSequence(connection => connection.IsConnected)
            .Returns(false)
            .Returns(true);
        SetupCachedString(ProductCacheKeys.GetDetailsKey(id), JsonSerializer.Serialize(cachedProduct));
        _innerMock.Setup(inner => inner.GetProductByProductIdAsync(id)).ReturnsAsync(databaseProduct);
        var decorator = CreateDecorator();

        (await decorator.GetProductByProductIdAsync(id)).Should().BeSameAs(databaseProduct);
        (await decorator.GetProductByProductIdAsync(id)).Should().BeEquivalentTo(cachedProduct);
        _innerMock.Verify(inner => inner.GetProductByProductIdAsync(id), Times.Once);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_WhenCachedJsonIsInvalid_ReadsDatabase()
    {
        var id = Guid.NewGuid();
        var product = new ProductResponse(id, "Database", 20, 4);
        SetupCachedString(ProductCacheKeys.GetDetailsKey(id), "not json");
        _innerMock.Setup(inner => inner.GetProductByProductIdAsync(id)).ReturnsAsync(product);

        var result = await CreateDecorator().GetProductByProductIdAsync(id);

        result.Should().BeSameAs(product);
        _innerMock.Verify(inner => inner.GetProductByProductIdAsync(id), Times.Once);
    }

    [Fact]
    public async Task GetProductsAsync_WhenFirstRedisReadFails_FallsBackWithoutLockOrFill()
    {
        SetupListResult();
        _cacheMock.Setup(cache => cache.GetAsync(ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis offline"));

        var result = await CreateDecorator().GetProductsAsync();

        result.Should().ContainSingle();
        _innerMock.Verify(inner => inner.GetProductsAsync(), Times.Once);
        _lockFactoryMock.Verify(factory => factory.CreateLockAsync(It.IsAny<string>()), Times.Never);
        VerifyNoCacheFill();
    }

    [Fact]
    public async Task GetProductsAsync_WhenLocalRecheckFails_FallsBackWithoutLockOrFill()
    {
        SetupListResult();
        _cacheMock.SetupSequence(cache => cache.GetAsync(ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null)
            .ThrowsAsync(new InvalidOperationException("Redis offline"));

        var result = await CreateDecorator().GetProductsAsync();

        result.Should().ContainSingle();
        _innerMock.Verify(inner => inner.GetProductsAsync(), Times.Once);
        _lockFactoryMock.Verify(factory => factory.CreateLockAsync(It.IsAny<string>()), Times.Never);
        VerifyNoCacheFill();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetProductsAsync_WhenLockCreationOrAcquisitionFails_FallsBackWithoutFill(bool acquireFails)
    {
        SetupListResult();
        SetupCachedString(ProductCacheKeys.AllProductsKey, null);
        if (acquireFails)
        {
            var distributedLock = new Mock<IDistributedLock>();
            distributedLock.Setup(lockInstance => lockInstance.AcquireAsync(
                    TimeSpan.FromSeconds(5), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException("Lock busy"));
            _lockFactoryMock.Setup(factory => factory.CreateLockAsync(It.IsAny<string>()))
                .ReturnsAsync(distributedLock.Object);
        }
        else
        {
            _lockFactoryMock.Setup(factory => factory.CreateLockAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Redis offline"));
        }

        var result = await CreateDecorator().GetProductsAsync();

        result.Should().ContainSingle();
        _innerMock.Verify(inner => inner.GetProductsAsync(), Times.Once);
        VerifyNoCacheFill();
    }

    [Fact]
    public async Task GetProductsAsync_WhenRedisReadUnderLockFails_FallsBackAndReleasesLock()
    {
        SetupListResult();
        _cacheMock.SetupSequence(cache => cache.GetAsync(ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null)
            .ReturnsAsync((byte[]?)null)
            .ThrowsAsync(new InvalidOperationException("Redis offline"));
        var handle = SetupAcquiredLock();

        var result = await CreateDecorator().GetProductsAsync();

        result.Should().ContainSingle();
        _innerMock.Verify(inner => inner.GetProductsAsync(), Times.Once);
        handle.Verify(lockHandle => lockHandle.DisposeAsync(), Times.Once);
        VerifyNoCacheFill();
    }

    [Fact]
    public async Task GetProductsAsync_WhenLockReleaseFails_PreservesDatabaseResult()
    {
        SetupListResult();
        SetupCachedString(ProductCacheKeys.AllProductsKey, null);
        var handle = SetupAcquiredLock();
        handle.Setup(lockHandle => lockHandle.DisposeAsync())
            .Returns(ValueTask.FromException(new InvalidOperationException("Redis offline")));

        var result = await CreateDecorator().GetProductsAsync();

        result.Should().ContainSingle();
        _innerMock.Verify(inner => inner.GetProductsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetProductsAsync_WhenDatabaseFailsDuringRedisOutage_PropagatesDatabaseException()
    {
        _connectionMock.SetupGet(connection => connection.IsConnected).Returns(false);
        var databaseError = new InvalidOperationException("Database offline");
        _innerMock.Setup(inner => inner.GetProductsAsync()).ThrowsAsync(databaseError);

        Func<Task> act = () => CreateDecorator().GetProductsAsync();

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(databaseError);
        _lockFactoryMock.Verify(factory => factory.CreateLockAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetProductsAsync_WhenCachePayloadIsMalformed_LoadsAndFillsFromDatabase()
    {
        SetupListResult();
        SetupCachedString(ProductCacheKeys.AllProductsKey, "not json");
        SetupAcquiredLock();

        var result = await CreateDecorator().GetProductsAsync();

        result.Should().ContainSingle();
        _innerMock.Verify(inner => inner.GetProductsAsync(), Times.Once);
        _cacheMock.Verify(cache => cache.SetAsync(ProductCacheKeys.AllProductsKey,
            It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProductsAsync_WhenCacheFillFails_ReturnsDatabaseList()
    {
        SetupListResult();
        SetupCachedString(ProductCacheKeys.AllProductsKey, null);
        SetupAcquiredLock();
        _cacheMock.Setup(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis offline"));

        var result = await CreateDecorator().GetProductsAsync();

        result.Should().ContainSingle();
        _innerMock.Verify(inner => inner.GetProductsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetProductsAsync_WhenDatabaseFailsUnderLock_PropagatesDatabaseException()
    {
        SetupCachedString(ProductCacheKeys.AllProductsKey, null);
        var handle = SetupAcquiredLock();
        handle.Setup(lockHandle => lockHandle.DisposeAsync())
            .Returns(ValueTask.FromException(new InvalidOperationException("Redis offline")));
        var databaseError = new InvalidOperationException("Database offline");
        _innerMock.Setup(inner => inner.GetProductsAsync()).ThrowsAsync(databaseError);

        Func<Task> act = () => CreateDecorator().GetProductsAsync();

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(databaseError);
        _innerMock.Verify(inner => inner.GetProductsAsync(), Times.Once);
    }

    private void SetupListResult()
    {
        ProductResponse[] products = [new(Guid.NewGuid(), "Database", 20, 4)];
        _innerMock.Setup(inner => inner.GetProductsAsync()).ReturnsAsync(products);
    }

    private Mock<IDistributedSynchronizationHandle> SetupAcquiredLock()
    {
        var handle = new Mock<IDistributedSynchronizationHandle>();
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock.Setup(lockInstance => lockInstance.AcquireAsync(
                TimeSpan.FromSeconds(5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(handle.Object);
        _lockFactoryMock.Setup(factory => factory.CreateLockAsync(It.IsAny<string>()))
            .ReturnsAsync(distributedLock.Object);
        return handle;
    }

    private void VerifyNoCacheFill() => _cacheMock.Verify(cache => cache.SetAsync(
        It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(),
        It.IsAny<CancellationToken>()), Times.Never);

    private void SetupCachedString(string key, string? value)
    {
        _cacheMock.Setup(x => x.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value == null ? null : Encoding.UTF8.GetBytes(value));
    }
}
