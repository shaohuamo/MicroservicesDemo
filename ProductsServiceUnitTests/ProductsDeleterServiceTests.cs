using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.ExternalServices.Abstractions;
using ProductsMicroservice.Core.Services;
using Xunit;

namespace ProductsMicroservice.Tests;

public class ProductsDeleterServiceTests
{
    private readonly Mock<IProductsRepository> _repoMock = new();
    private readonly Mock<ITestMicroserviceClient> _clientMock = new();
    private readonly Mock<ILogger<ProductsDeleterService>> _loggerMock = new();

    private readonly ProductsDeleterService _service;

    public ProductsDeleterServiceTests()
    {
        _service = new ProductsDeleterService(
            _repoMock.Object,
            _clientMock.Object,
            _loggerMock.Object
        );
    }

    #region Success

    [Fact]
    public async Task DeleteProductAsync_ShouldDeleteProduct_AndCallDownstream()
    {
        var productId = Guid.NewGuid();

        _repoMock.Setup(x => x.DeleteProductAsync(productId))
            .ReturnsAsync(true);

        _clientMock.Setup(x => x.DeleteProductRelatedInfoByProductIdAsync(productId))
            .ReturnsAsync(true);

        var result = await _service.DeleteProductAsync(productId);

        result.Should().BeTrue();

        _repoMock.Verify(x => x.DeleteProductAsync(productId), Times.Once);

        _clientMock.Verify(x =>
            x.DeleteProductRelatedInfoByProductIdAsync(productId), Times.Once);
    }

    #endregion

    #region Product Not Found

    [Fact]
    public async Task DeleteProductAsync_ShouldReturnFalse_WhenProductNotFound()
    {
        var productId = Guid.NewGuid();

        _repoMock.Setup(x => x.DeleteProductAsync(productId))
            .ReturnsAsync(false);

        var result = await _service.DeleteProductAsync(productId);

        result.Should().BeFalse();

        // downstream should NOT be called
        _clientMock.Verify(x =>
            x.DeleteProductRelatedInfoByProductIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    #endregion

    #region Downstream Failure (Returns False)

    [Fact]
    public async Task DeleteProductAsync_ShouldStillReturnTrue_WhenDownstreamFails()
    {
        var productId = Guid.NewGuid();

        _repoMock.Setup(x => x.DeleteProductAsync(productId))
            .ReturnsAsync(true);

        _clientMock.Setup(x => x.DeleteProductRelatedInfoByProductIdAsync(productId))
            .ReturnsAsync(false);

        var result = await _service.DeleteProductAsync(productId);

        // IMPORTANT: service returns DB result only
        result.Should().BeTrue();

        _clientMock.Verify(x =>
            x.DeleteProductRelatedInfoByProductIdAsync(productId), Times.Once);
    }

    #endregion

    #region Repository Exception

    [Fact]
    public async Task DeleteProductAsync_ShouldThrow_WhenRepositoryThrows()
    {
        var productId = Guid.NewGuid();

        _repoMock.Setup(x => x.DeleteProductAsync(productId))
            .ThrowsAsync(new Exception("DB error"));

        Func<Task> act = async () => await _service.DeleteProductAsync(productId);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("DB error");
    }

    #endregion

    #region Downstream Exception

    [Fact]
    public async Task DeleteProductAsync_ShouldThrow_WhenDownstreamThrows()
    {
        var productId = Guid.NewGuid();

        _repoMock.Setup(x => x.DeleteProductAsync(productId))
            .ReturnsAsync(true);

        _clientMock.Setup(x => x.DeleteProductRelatedInfoByProductIdAsync(productId))
            .ThrowsAsync(new Exception("API error"));

        Func<Task> act = async () => await _service.DeleteProductAsync(productId);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("API error");
    }

    #endregion
}