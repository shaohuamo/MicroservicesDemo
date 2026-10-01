using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Observability;

namespace ProductsMicroservice.Tests;

public class ProductsUpdaterTelemetryDecoratorTests
{
    private readonly Guid _key = Guid.NewGuid();
    private readonly Mock<IProductsUpdaterService> _innerMock = new();
    private readonly Mock<ILogger<ProductsUpdaterTelemetryDecorator>> _loggerMock = new();
    private readonly ProductsUpdaterTelemetryDecorator _decorator;

    public ProductsUpdaterTelemetryDecoratorTests()
    {
        _decorator = new ProductsUpdaterTelemetryDecorator(_innerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldThrow_WhenRequestIsNull()
    {
        Func<Task> act = () => _decorator.UpdateProductAsync(null!, _key);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldReturnInnerResult()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid(), DisplayName = "Product" };
        var response = new ProductResponse();
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key)).ReturnsAsync(new ProductUpdateResult(response, false));

        var result = await _decorator.UpdateProductAsync(request, _key);

        result.Product.Should().BeSameAs(response);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldRethrowInnerException()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid(), DisplayName = "Product" };
        _innerMock.Setup(x => x.UpdateProductAsync(request, _key))
            .ThrowsAsync(new InvalidOperationException("failure"));

        Func<Task> act = () => _decorator.UpdateProductAsync(request, _key);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("failure");
    }
}
