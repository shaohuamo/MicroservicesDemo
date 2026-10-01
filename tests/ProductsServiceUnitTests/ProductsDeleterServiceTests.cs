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

public class ProductsDeleterServiceTests
{
    private readonly Guid _key = Guid.NewGuid();
    private readonly Mock<IProductsRepository> _repository = new();
    private readonly Mock<IProductOperationOutboxWriter> _outbox = new();
    private readonly ProductsDeleterService _service;

    public ProductsDeleterServiceTests()
    {
        var context = new Mock<IProductOperationContextAccessor>();
        context.Setup(x => x.GetCurrent()).Returns(new ProductOperationContext("user-1", "user@example.com", "en", "correlation-1"));
        _service = new ProductsDeleterService(_repository.Object, _outbox.Object, context.Object);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldPrepareSuccessNotification()
    {
        var id = Guid.NewGuid();
        _repository.Setup(x => x.DeleteProductAsync(id, 3, default)).ReturnsAsync(new Product { ProductId = id, ProductName = "DELETED", DisplayName = "Deleted", Version = 3 });

        await _service.DeleteProductAsync(id, 3, _key);
        _outbox.Verify(x => x.WriteAsync(It.Is<ProductOperationResultMessage>(m =>
            m.Operation == ProductOperation.Delete && m.Status == ProductOperationStatus.Success && m.ProductId == id), default), Times.Once);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldThrowAndNotWriteOutbox_WhenProductIsMissing()
    {
        var id = Guid.NewGuid();
        _repository.Setup(x => x.DeleteProductAsync(id, 1, default)).ReturnsAsync((Product?)null);

        await FluentActions.Invoking(() => _service.DeleteProductAsync(id, 1, _key))
            .Should().ThrowAsync<ProductNotFoundException>();
        _outbox.Verify(x => x.WriteAsync(It.IsAny<ProductOperationResultMessage>(), default), Times.Never);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldNotWriteOutbox_WhenRepositoryThrows()
    {
        var id = Guid.NewGuid();
        _repository.Setup(x => x.DeleteProductAsync(id, 1, default)).ThrowsAsync(new InvalidOperationException("DB error"));

        await FluentActions.Invoking(() => _service.DeleteProductAsync(id, 1, _key)).Should().ThrowAsync<InvalidOperationException>();
        _outbox.Verify(x => x.WriteAsync(It.IsAny<ProductOperationResultMessage>(), default), Times.Never);
    }
}
