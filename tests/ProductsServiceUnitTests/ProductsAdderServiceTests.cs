using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.MessageQueue.Abstractions;
using ProductsMicroservice.Core.MessageQueue.Messages;
using ProductsMicroservice.Core.Services;

namespace ProductsMicroservice.Tests;

public class ProductsAdderServiceTests
{
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly Mock<IProductsRepository> _repoMock = new();
    private readonly Mock<IProductMessagePublisher> _publisherMock = new();
    private readonly Mock<IConfiguration> _configMock = new();
    private readonly Mock<ILogger<ProductsAdderService>> _loggerMock = new();

    private readonly ProductsAdderService _service;

    private const string RoutingKey = "product.add";

    public ProductsAdderServiceTests()
    {
        _configMock.Setup(x => x["RabbitMQ_Products_RoutingKey"])
            .Returns(RoutingKey);

        _service = new ProductsAdderService(
            _mapperMock.Object,
            _repoMock.Object,
            _publisherMock.Object,
            _configMock.Object,
            _loggerMock.Object
        );
    }

    #region Null Input

    [Fact]
    public async Task AddProductAsync_ShouldThrow_WhenRequestIsNull()
    {
        Func<Task> act = async () => await _service.AddProductAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Success

    [Fact]
    public async Task AddProductAsync_ShouldAddProduct_AndPublishMessage()
    {
        var request = CreateRequest();

        var product = new Product
        {
            ProductId = Guid.NewGuid(),
            ProductName = request.ProductName,
            UnitPrice = request.UnitPrice,
            QuantityInStock = request.QuantityInStock
        };

        var response = new ProductResponse();

        _mapperMock.Setup(x => x.Map<Product>(request)).Returns(product);
        _repoMock.Setup(x => x.AddProductAsync(product)).ReturnsAsync(product);
        _mapperMock.Setup(x => x.Map<ProductResponse>(product)).Returns(response);

        var result = await _service.AddProductAsync(request);

        result.Should().NotBeNull();

        _repoMock.Verify(x => x.AddProductAsync(product), Times.Once);

        _publisherMock.Verify(x => x.PublishAsync(
            RoutingKey,
            It.Is<ProductAddMessage>(msg =>
                msg.ProductId == product.ProductId &&
                msg.ProductName == product.ProductName &&
                msg.UnitPrice == product.UnitPrice &&
                msg.QuantityInStock == product.QuantityInStock
            )), Times.Once);
    }

    #endregion

    #region Repository Returns Null

    [Fact]
    public async Task AddProductAsync_ShouldReturnNull_WhenRepositoryReturnsNull()
    {
        var request = CreateRequest();

        _mapperMock.Setup(x => x.Map<Product>(request))
            .Returns(new Product());

        _repoMock.Setup(x => x.AddProductAsync(It.IsAny<Product>()))
            .ReturnsAsync((Product?)null);

        var result = await _service.AddProductAsync(request);

        result.Should().BeNull();

        _publisherMock.Verify(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<ProductAddMessage>()), Times.Never);
    }

    #endregion

    #region Repository Exception

    [Fact]
    public async Task AddProductAsync_ShouldThrow_WhenRepositoryThrows()
    {
        var request = CreateRequest();

        _mapperMock.Setup(x => x.Map<Product>(request))
            .Returns(new Product());

        _repoMock.Setup(x => x.AddProductAsync(It.IsAny<Product>()))
            .ThrowsAsync(new Exception("DB error"));

        Func<Task> act = async () => await _service.AddProductAsync(request);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("DB error");
    }

    #endregion

    #region Publisher Exception

    [Fact]
    public async Task AddProductAsync_ShouldThrow_WhenPublisherFails()
    {
        var request = CreateRequest();

        var product = new Product { ProductId = Guid.NewGuid() };

        _mapperMock.Setup(x => x.Map<Product>(request)).Returns(product);
        _repoMock.Setup(x => x.AddProductAsync(product)).ReturnsAsync(product);

        _publisherMock.Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<ProductAddMessage>()))
            .ThrowsAsync(new Exception("MQ failure"));

        Func<Task> act = async () => await _service.AddProductAsync(request);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("MQ failure");
    }

    #endregion

    #region Configuration Usage

    [Fact]
    public async Task AddProductAsync_ShouldUseRoutingKey_FromConfiguration()
    {
        var request = CreateRequest();
        var product = new Product { ProductId = Guid.NewGuid() };

        _mapperMock.Setup(x => x.Map<Product>(request)).Returns(product);
        _repoMock.Setup(x => x.AddProductAsync(product)).ReturnsAsync(product);

        await _service.AddProductAsync(request);

        _publisherMock.Verify(x => x.PublishAsync(
            RoutingKey,
            It.IsAny<ProductAddMessage>()), Times.Once);
    }

    #endregion

    #region Helpers

    private static ProductAddRequest CreateRequest()
    {
        return new ProductAddRequest
        {
            ProductName = "Test Product",
            UnitPrice = 100,
            QuantityInStock = 10
        };
    }

    #endregion
}