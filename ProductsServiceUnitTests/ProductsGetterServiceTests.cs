using AutoMapper;
using FluentAssertions;
using Moq;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Services;

namespace ProductsMicroservice.Tests;

public class ProductsGetterServiceTests
{
    private readonly Mock<IProductsRepository> _repoMock = new();
    private readonly Mock<IMapper> _mapperMock = new();

    private readonly ProductsGetterService _service;

    public ProductsGetterServiceTests()
    {
        _service = new ProductsGetterService(
            _repoMock.Object,
            _mapperMock.Object
        );
    }

    #region GetProductsAsync

    [Fact]
    public async Task GetProductsAsync_ShouldReturnMappedProducts()
    {
        var products = new List<Product>
        {
            new Product(),
            new Product()
        };

        var mapped = new List<ProductResponse>
        {
            new ProductResponse(),
            new ProductResponse()
        };

        _repoMock.Setup(x => x.GetProductsAsync())
            .ReturnsAsync(products);

        _mapperMock.Setup(x => x.Map<IEnumerable<ProductResponse>>(products))
            .Returns(mapped);

        var result = await _service.GetProductsAsync();

        result.Should().BeEquivalentTo(mapped);

        _repoMock.Verify(x => x.GetProductsAsync(), Times.Once);
        _mapperMock.Verify(x => x.Map<IEnumerable<ProductResponse>>(products), Times.Once);
    }

    #endregion

    #region GetProductByProductIdAsync - Found

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldReturnMappedProduct_WhenFound()
    {
        var productId = Guid.NewGuid();

        var product = new Product { ProductId = productId };
        var mapped = new ProductResponse();

        _repoMock.Setup(x => x.GetProductByProductIdAsync(productId))
            .ReturnsAsync(product);

        _mapperMock.Setup(x => x.Map<ProductResponse>(product))
            .Returns(mapped);

        var result = await _service.GetProductByProductIdAsync(productId);

        result.Should().Be(mapped);

        _repoMock.Verify(x => x.GetProductByProductIdAsync(productId), Times.Once);
        _mapperMock.Verify(x => x.Map<ProductResponse>(product), Times.Once);
    }

    #endregion

    #region GetProductByProductIdAsync - Not Found

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldReturnNull_WhenNotFound()
    {
        var productId = Guid.NewGuid();

        _repoMock.Setup(x => x.GetProductByProductIdAsync(productId))
            .ReturnsAsync((Product?)null);

        var result = await _service.GetProductByProductIdAsync(productId);

        result.Should().BeNull();

        _mapperMock.Verify(x => x.Map<ProductResponse>(It.IsAny<Product>()), Times.Never);
    }

    #endregion

    #region Repository Exception

    [Fact]
    public async Task GetProductsAsync_ShouldThrow_WhenRepositoryThrows()
    {
        _repoMock.Setup(x => x.GetProductsAsync())
            .ThrowsAsync(new Exception("DB error"));

        Func<Task> act = async () => await _service.GetProductsAsync();

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("DB error");
    }

    #endregion
}