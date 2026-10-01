using AutoMapper;
using CommonService.Messages;
using FluentAssertions;
using Moq;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Messaging.OutboxWriterContracts;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Core.Services;

namespace ProductsMicroservice.Tests;

public class ProductsAdderServiceTests
{
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IProductsRepository> _repository = new();
    private readonly Mock<IProductOperationOutboxWriter> _outbox = new();
    private readonly ProductsAdderService _service;
    private readonly Guid _idempotencyKey = Guid.NewGuid();

    public ProductsAdderServiceTests()
    {
        var context = new Mock<IProductOperationContextAccessor>();
        context.Setup(x => x.GetCurrent()).Returns(new ProductOperationContext("user-1", "user@example.com", "en", "correlation-1"));
        _service = new ProductsAdderService(_mapper.Object, _repository.Object, _outbox.Object, context.Object);
    }

    [Fact]
    public async Task AddProductAsync_ShouldThrow_WhenRequestIsNull() =>
        await FluentActions.Invoking(() => _service.AddProductAsync(null!, _idempotencyKey))
            .Should().ThrowAsync<ArgumentNullException>();

    [Fact]
    public async Task AddProductAsync_ShouldPrepareProductAndSuccessOutbox()
    {
        var request = new ProductAddRequest { DisplayName = "Test", UnitPrice = 10, QuantityInStock = 2 };
        var product = new Product { ProductId = Guid.NewGuid(), DisplayName = request.DisplayName!, ProductName = "TEST" };
        var response = new ProductResponse();
        _mapper.Setup(x => x.Map<Product>(request)).Returns(product);
        _repository.Setup(x => x.ProductNameExistsAsync("TEST", null, default)).ReturnsAsync(false);
        _repository.Setup(x => x.AddProductAsync(product, default)).ReturnsAsync(product);
        _mapper.Setup(x => x.Map<ProductResponse>(product)).Returns(response);

        ProductAddResult result = await _service.AddProductAsync(request, _idempotencyKey);
        result.Product.Should().BeSameAs(response);
        result.IsReplay.Should().BeFalse();
        product.DisplayName.Should().Be("Test");
        product.ProductName.Should().Be("TEST");

        _outbox.Verify(x => x.WriteAsync(It.Is<ProductOperationResultMessage>(m =>
            m.Operation == ProductOperation.Add && m.Status == ProductOperationStatus.Success &&
            m.ProductId == product.ProductId && m.UserId == "user-1"), default), Times.Once);

    }

    [Fact]
    public async Task AddProductAsync_ShouldNotWriteOutbox_WhenRepositoryThrows()
    {
        var request = new ProductAddRequest { DisplayName = "Test" };
        _mapper.Setup(x => x.Map<Product>(request)).Returns(new Product { ProductId = Guid.NewGuid(), DisplayName = "Test" });
        _repository.Setup(x => x.AddProductAsync(It.IsAny<Product>(), default)).ThrowsAsync(new InvalidOperationException("DB error"));

        await FluentActions.Invoking(() => _service.AddProductAsync(request, _idempotencyKey))
            .Should().ThrowAsync<InvalidOperationException>();

        _outbox.Verify(x => x.WriteAsync(It.IsAny<ProductOperationResultMessage>(), default), Times.Never);
    }

    [Fact]
    public async Task AddProductAsync_ShouldThrowAndNotWriteOutbox_WhenNameExists()
    {
        var request = new ProductAddRequest { DisplayName = "Test" };
        _mapper.Setup(x => x.Map<Product>(request)).Returns(
            new Product { ProductId = Guid.NewGuid(), DisplayName = "Test" });
        _repository.Setup(x => x.ProductNameExistsAsync("TEST", null, default)).ReturnsAsync(true);

        await FluentActions.Invoking(() => _service.AddProductAsync(request, _idempotencyKey))
            .Should().ThrowAsync<ProductAlreadyExistsException>();

        _repository.Verify(x => x.AddProductAsync(It.IsAny<Product>(), default), Times.Never);
        _outbox.Verify(x => x.WriteAsync(It.IsAny<ProductOperationResultMessage>(), default), Times.Never);
    }

}
