using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Caching;

namespace ProductsMicroservice.Tests;

public class ProductsAdderCachingDecoratorTests
{
    private readonly Guid _key = Guid.NewGuid();
    private readonly Mock<IProductsAdderService> _innerMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<ILogger<ProductsAdderCachingDecorator>> _loggerMock = new();
    private readonly ProductsAdderCachingDecorator _decorator;

    public ProductsAdderCachingDecoratorTests()
    {
        _decorator = new ProductsAdderCachingDecorator(
            _innerMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task AddProductAsync_ShouldThrow_WhenRequestIsNull()
    {
        Func<Task> act = () => _decorator.AddProductAsync(null!, _key);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddProductAsync_ShouldInvalidateAllProductsCache_WhenSuccessful()
    {
        var request = new ProductAddRequest();
        var response = new ProductResponse(Guid.NewGuid(), "Product", 10, 1);
        var addResult = new ProductAddResult(response, false);
        _innerMock.Setup(x => x.AddProductAsync(request, _key)).ReturnsAsync(addResult);

        var result = await _decorator.AddProductAsync(request, _key);

        result.Should().BeSameAs(addResult);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddProductAsync_ShouldNotInvalidateCache_WhenInnerThrows()
    {
        var request = new ProductAddRequest();
        _innerMock.Setup(x => x.AddProductAsync(request, _key))
            .ThrowsAsync(new InvalidOperationException("save failed"));

        Func<Task> action = () => _decorator.AddProductAsync(request, _key);

        await action.Should().ThrowAsync<InvalidOperationException>();
        _cacheMock.Verify(x => x.RemoveAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddProductAsync_ShouldReturnResponse_WhenCacheInvalidationFails()
    {
        var request = new ProductAddRequest();
        var response = new ProductResponse();
        var addResult = new ProductAddResult(response, false);
        _innerMock.Setup(x => x.AddProductAsync(request, _key)).ReturnsAsync(addResult);
        _cacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.AllProductsKey,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var result = await _decorator.AddProductAsync(request, _key);

        result.Should().BeSameAs(addResult);
    }

    [Fact]
    public async Task AddProductAsync_ShouldNotInvalidateCache_WhenResponseIsReplay()
    {
        var request = new ProductAddRequest();
        var addResult = new ProductAddResult(new ProductResponse(), true);
        _innerMock.Setup(x => x.AddProductAsync(request, _key)).ReturnsAsync(addResult);

        var result = await _decorator.AddProductAsync(request, _key);

        result.Should().BeSameAs(addResult);
        _cacheMock.Verify(x => x.RemoveAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
