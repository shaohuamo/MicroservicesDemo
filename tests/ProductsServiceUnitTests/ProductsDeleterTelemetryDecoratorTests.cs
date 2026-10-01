using ProductsMicroservice.Core.DTO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Observability;

namespace ProductsMicroservice.Tests;

public class ProductsDeleterTelemetryDecoratorTests
{
    private readonly Guid _key = Guid.NewGuid();
    private readonly Mock<IProductsDeleterService> _innerMock = new();
    private readonly Mock<ILogger<ProductsDeleterTelemetryDecorator>> _loggerMock = new();
    private readonly ProductsDeleterTelemetryDecorator _decorator;

    public ProductsDeleterTelemetryDecoratorTests()
    {
        _decorator = new ProductsDeleterTelemetryDecorator(_innerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldThrow_WhenProductIdIsEmpty()
    {
        Func<Task> act = () => _decorator.DeleteProductAsync(Guid.Empty, 1, _key);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldComplete_WhenInnerSucceeds()
    {
        var productId = Guid.NewGuid();
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1, _key))
            .ReturnsAsync(new ProductDeleteResult(true, false));

        await _decorator.DeleteProductAsync(productId, 1, _key);

        _innerMock.Verify(x => x.DeleteProductAsync(productId, 1, _key), Times.Once);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldRethrowInnerException()
    {
        var productId = Guid.NewGuid();
        _innerMock.Setup(x => x.DeleteProductAsync(productId, 1, _key))
            .ThrowsAsync(new InvalidOperationException("failure"));

        Func<Task> act = () => _decorator.DeleteProductAsync(productId, 1, _key);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("failure");
    }
}
