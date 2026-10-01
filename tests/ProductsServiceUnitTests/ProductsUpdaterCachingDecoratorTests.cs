using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Caching;
using ProductsMicroservice.Infrastructure.Options;
using System.Text;
using System.Text.Json;

namespace ProductsMicroservice.Tests;

public class ProductsUpdaterCachingDecoratorTests
{
    private readonly Guid _key = Guid.NewGuid();
    private readonly Mock<IProductsUpdaterService> _innerMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<IDistributedCache> _delayedCacheMock = new();
    private readonly Mock<ILogger<ProductsUpdaterCachingDecorator>> _loggerMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly TaskCompletionSource _delayedDeleteCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ProductsUpdaterCachingDecoratorTests()
    {
        _cacheMock.Setup(x => x.RemoveAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _delayedCacheMock.Setup(x => x.RemoveAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _delayedCacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()))
            .Callback(() => _delayedDeleteCompleted.TrySetResult())
            .Returns(Task.CompletedTask);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(IDistributedCache)))
            .Returns(_delayedCacheMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ILogger<ProductsUpdaterCachingDecorator>)))
            .Returns(_loggerMock.Object);
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.SetupGet(x => x.ServiceProvider).Returns(serviceProviderMock.Object);
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(scopeMock.Object);
    }

    private ProductsUpdaterCachingDecorator CreateDecorator()
    {
        return new ProductsUpdaterCachingDecorator(
            _innerMock.Object,
            _cacheMock.Object,
            Options.Create(new RedisOptions { DelayedDeleteMs = 0 }),
            Options.Create(new CacheOptions { NullValuePlaceholder = "missing" }),
            _loggerMock.Object,
            _scopeFactoryMock.Object);
    }

    private static byte[] CachedProduct(Guid productId) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new ProductResponse(productId, "Cached", 1, 1, 1)));

    [Fact]
    public async Task UpdateProductAsync_ShouldThrow_WhenRequestIsNull()
    {
        Func<Task> act = () => CreateDecorator().UpdateProductAsync(null!, _key);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateProductAsync_WhenReplayed_ShouldSkipInvalidationAndDelayedScope()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var replay = new ProductUpdateResult(new ProductResponse(request.ProductId, "Original", 1, 1, 2), true)
            { Source = IdempotencyResultSource.Redis };
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key)).ReturnsAsync(replay);

        (await CreateDecorator().UpdateProductAsync(request, _key)).Should().BeSameAs(replay);

        _cacheMock.VerifyNoOtherCalls();
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
        _delayedCacheMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldNotRemoveCachesBeforeCallingInnerService()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var response = new ProductResponse(request.ProductId, "Updated", 1, 1, 2);
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key))
            .Callback(() => _cacheMock.Verify(x => x.RemoveAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never))
            .ReturnsAsync(new ProductUpdateResult(response, false));

        var result = await CreateDecorator().UpdateProductAsync(request, _key);
        await _delayedDeleteCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result.Product.Should().BeSameAs(response);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldInvalidateBothCaches_WhenVersionConflicts()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var failure = new ProductConcurrencyException(request.ProductId);
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key)).ThrowsAsync(failure);

        var thrown = await Record.ExceptionAsync(() => CreateDecorator().UpdateProductAsync(request, _key));

        thrown.Should().BeSameAs(failure);
        _innerMock.Verify(x => x.UpdateProductAsync(request, _key), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.GetAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldInvalidateBothCaches_WhenNotFoundAndDetailCached()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var failure = new ProductNotFoundException(request.ProductId);
        var detailKey = ProductCacheKeys.GetDetailsKey(request.ProductId);
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key)).ThrowsAsync(failure);
        _cacheMock.Setup(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CachedProduct(request.ProductId));

        var thrown = await Record.ExceptionAsync(() => CreateDecorator().UpdateProductAsync(request, _key));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("missing")]
    public async Task UpdateProductAsync_ShouldInvalidateListOnly_WhenNotFoundWithoutPositiveDetail(string? cachedValue)
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var failure = new ProductNotFoundException(request.ProductId);
        var detailKey = ProductCacheKeys.GetDetailsKey(request.ProductId);
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key)).ThrowsAsync(failure);
        _cacheMock.Setup(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedValue is null ? null : Encoding.UTF8.GetBytes(cachedValue));

        var thrown = await Record.ExceptionAsync(() => CreateDecorator().UpdateProductAsync(request, _key));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(
            detailKey, It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldKeepNotFoundAndInvalidateList_WhenCacheLookupFails()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var failure = new ProductNotFoundException(request.ProductId);
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key)).ThrowsAsync(failure);
        _cacheMock.Setup(x => x.GetAsync(
                ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var thrown = await Record.ExceptionAsync(() => CreateDecorator().UpdateProductAsync(request, _key));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateProductAsync_ShouldKeepOriginalFailureAndTryBothKeys_WhenRemovalFails(bool notFound)
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var detailKey = ProductCacheKeys.GetDetailsKey(request.ProductId);
        Exception failure = notFound
            ? new ProductNotFoundException(request.ProductId)
            : new ProductConcurrencyException(request.ProductId);
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key)).ThrowsAsync(failure);
        if (notFound)
        {
            _cacheMock.Setup(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CachedProduct(request.ProductId));
        }
        _cacheMock.Setup(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var thrown = await Record.ExceptionAsync(() => CreateDecorator().UpdateProductAsync(request, _key));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldNotInvalidateCaches_WhenNameConflictOccurs()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var failure = new ProductAlreadyExistsException("duplicate");
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key)).ThrowsAsync(failure);

        var thrown = await Record.ExceptionAsync(() => CreateDecorator().UpdateProductAsync(request, _key));

        thrown.Should().BeSameAs(failure);
        _cacheMock.Verify(x => x.RemoveAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldRemoveCachesImmediatelyAndAfterDelay_WhenSuccessful()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var response = new ProductResponse(request.ProductId, "Updated", 20, 3);
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key)).ReturnsAsync(new ProductUpdateResult(response, false));

        var result = await CreateDecorator().UpdateProductAsync(request, _key);
        await _delayedDeleteCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result.Product.Should().BeSameAs(response);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId),
            It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey,
            It.IsAny<CancellationToken>()), Times.Once);
        _delayedCacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId),
            It.IsAny<CancellationToken>()), Times.Once);
        _delayedCacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UpdateProductAsync_ShouldReturnResponseAndScheduleDelay_WhenImmediateInvalidationFails(
        bool detailFails, bool listFails)
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var response = new ProductResponse(request.ProductId, "Updated", 1, 1, 2);
        _cacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()))
            .Returns(detailFails
                ? Task.FromException(new InvalidOperationException("detail cache unavailable"))
                : Task.CompletedTask);
        _cacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()))
            .Returns(listFails
                ? Task.FromException(new InvalidOperationException("list cache unavailable"))
                : Task.CompletedTask);
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key)).ReturnsAsync(new ProductUpdateResult(response, false));

        var result = await CreateDecorator().UpdateProductAsync(request, _key);
        await _delayedDeleteCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result.Product.Should().BeSameAs(response);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
        _delayedCacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()), Times.Once);
        _delayedCacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldTryBothDelayedKeys_WhenFirstDelayedRemovalFails()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var response = new ProductResponse(request.ProductId, "Updated", 1, 1, 2);
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key)).ReturnsAsync(new ProductUpdateResult(response, false));
        _delayedCacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var result = await CreateDecorator().UpdateProductAsync(request, _key);
        await _delayedDeleteCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result.Product.Should().BeSameAs(response);
        _delayedCacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId), It.IsAny<CancellationToken>()), Times.Once);
        _delayedCacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    #region Parallel invalidation

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateProductAsync_StartsBothRemovalsBeforeEitherCompletes(bool versionConflict)
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var response = new ProductResponse(request.ProductId, "Updated", 1, 1, 2);
        var failure = new ProductConcurrencyException(request.ProductId);
        var detailKey = ProductCacheKeys.GetDetailsKey(request.ProductId);
        var detailRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key))
            .Returns(versionConflict ? Task.FromException<ProductUpdateResult>(failure) : Task.FromResult(new ProductUpdateResult(response, false)));
        _cacheMock.Setup(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()))
            .Returns(detailRemoval.Task);
        _cacheMock.Setup(x => x.RemoveAsync(ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()))
            .Returns(listRemoval.Task);

        var operation = Record.ExceptionAsync(() => CreateDecorator().UpdateProductAsync(request, _key));
        try
        {
            _cacheMock.Verify(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()), Times.Once);
            _cacheMock.Verify(x => x.RemoveAsync(ProductCacheKeys.AllProductsKey,
                It.IsAny<CancellationToken>()), Times.Once);
            operation.IsCompleted.Should().BeFalse();
            detailRemoval.SetResult();
            operation.IsCompleted.Should().BeFalse("the list removal is still pending");
        }
        finally
        {
            detailRemoval.TrySetResult();
            listRemoval.TrySetResult();
            await operation;
            if (!versionConflict) await _delayedDeleteCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        (await operation).Should().BeSameAs(versionConflict ? failure : null);
    }

    [Fact]
    public async Task UpdateProductAsync_NotFound_StartsDetailLookupWhileListRemovalIsPending()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var detailKey = ProductCacheKeys.GetDetailsKey(request.ProductId);
        var failure = new ProductNotFoundException(request.ProductId);
        var listRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lookup = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var detailRemovalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key)).ThrowsAsync(failure);
        _cacheMock.Setup(x => x.RemoveAsync(ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()))
            .Returns(listRemoval.Task);
        _cacheMock.Setup(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>())).Returns(lookup.Task);
        _cacheMock.Setup(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()))
            .Callback(() => detailRemovalStarted.TrySetResult()).Returns(Task.CompletedTask);

        var operation = Record.ExceptionAsync(() => CreateDecorator().UpdateProductAsync(request, _key));
        try
        {
            _cacheMock.Verify(x => x.GetAsync(detailKey, It.IsAny<CancellationToken>()), Times.Once);
            _cacheMock.Verify(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()), Times.Never);
            lookup.SetResult(CachedProduct(request.ProductId));
            await detailRemovalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            operation.IsCompleted.Should().BeFalse("the list removal is still pending");
        }
        finally
        {
            lookup.TrySetResult(null);
            listRemoval.TrySetResult();
            await operation;
        }

        (await operation).Should().BeSameAs(failure);
    }

    [Fact]
    public async Task UpdateProductAsync_DelayedInvalidation_StartsBothRemovalsBeforeEitherCompletes()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var response = new ProductResponse(request.ProductId, "Updated", 1, 1, 2);
        var detailKey = ProductCacheKeys.GetDetailsKey(request.ProductId);
        var detailRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listRemovalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scopeDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key)).ReturnsAsync(new ProductUpdateResult(response, false));
        _delayedCacheMock.Setup(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()))
            .Returns(detailRemoval.Task);
        _delayedCacheMock.Setup(x => x.RemoveAsync(ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()))
            .Callback(() => listRemovalStarted.TrySetResult()).Returns(Task.CompletedTask);
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(IDistributedCache))).Returns(_delayedCacheMock.Object);
        serviceProvider.Setup(x => x.GetService(typeof(ILogger<ProductsUpdaterCachingDecorator>)))
            .Returns(_loggerMock.Object);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(serviceProvider.Object);
        scope.Setup(x => x.Dispose()).Callback(() => scopeDisposed.TrySetResult());
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(scope.Object);

        try
        {
            (await CreateDecorator().UpdateProductAsync(request, _key)).Product.Should().BeSameAs(response);
            await listRemovalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            _delayedCacheMock.Verify(x => x.RemoveAsync(detailKey,
                It.IsAny<CancellationToken>()), Times.Once);
            detailRemoval.Task.IsCompleted.Should().BeFalse();
            scopeDisposed.Task.IsCompleted.Should().BeFalse("both removals must finish before disposing the scope");
        }
        finally
        {
            detailRemoval.TrySetResult();
            await scopeDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    #endregion
}
