using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Observability;

namespace ProductsMicroservice.Tests;

public class ProductsAdderTelemetryDecoratorTests
{
    private readonly Guid _key = Guid.NewGuid();
    private readonly Mock<IProductsAdderService> _innerMock = new();
    private readonly Mock<ILogger<ProductsAdderTelemetryDecorator>> _loggerMock = new();
    private readonly ProductsAdderTelemetryDecorator _decorator;

    public ProductsAdderTelemetryDecoratorTests()
    {
        _decorator = new ProductsAdderTelemetryDecorator(_innerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task AddProductAsync_ShouldThrow_WhenRequestIsNull()
    {
        Func<Task> act = () => _decorator.AddProductAsync(null!, _key);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddProductAsync_ShouldReturnInnerResponse()
    {
        var request = new ProductAddRequest { DisplayName = "Product" };
        var response = new ProductResponse(Guid.NewGuid(), "Product", 10, 2);
        var addResult = new ProductAddResult(response, false);
        _innerMock.Setup(x => x.AddProductAsync(request, _key)).ReturnsAsync(addResult);

        var result = await _decorator.AddProductAsync(request, _key);

        result.Should().BeSameAs(addResult);
    }

    [Fact]
    public async Task AddProductAsync_ShouldRethrowInnerException()
    {
        var request = new ProductAddRequest { DisplayName = "Product" };
        _innerMock.Setup(x => x.AddProductAsync(request, _key))
            .ThrowsAsync(new InvalidOperationException("failure"));

        Func<Task> act = () => _decorator.AddProductAsync(request, _key);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("failure");
    }
}
