using AutoMapper;
using CommonService.Messages;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Messaging.OutboxWriterContracts;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Core.Services;

namespace ProductsMicroservice.Tests;

public class ProductsUpdaterServiceTests
{
    private readonly Mock<IProductsRepository> _repository = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IProductOperationOutboxWriter> _outbox = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ProductsUpdaterService _service;

    public ProductsUpdaterServiceTests()
    {
        var context = new Mock<IProductOperationContextAccessor>();
        context.Setup(x => x.GetCurrent()).Returns(new ProductOperationContext("user-1", "user@example.com", "en", "correlation-1"));
        _service = new ProductsUpdaterService(_repository.Object, _mapper.Object, _outbox.Object,
            _unitOfWork.Object, context.Object, Mock.Of<ILogger<ProductsUpdaterService>>());
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldThrow_WhenRequestIsNull() =>
        await FluentActions.Invoking(() => _service.UpdateProductAsync(null!)).Should().ThrowAsync<ArgumentNullException>();

    [Fact]
    public async Task UpdateProductAsync_ShouldCommitSuccessNotification()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid(), DisplayName = "Updated", Version = 6 };
        var product = new Product { ProductId = request.ProductId, DisplayName = request.DisplayName!, Version = 6 };
        var updated = new Product { ProductId = request.ProductId, DisplayName = request.DisplayName!, ProductName = "UPDATED", Version = 7 };
        var response = new ProductResponse();
        _mapper.Setup(x => x.Map<Product>(request)).Returns(product);
        _repository.Setup(x => x.GetProductByProductIdAsync(request.ProductId)).ReturnsAsync(
            new Product { ProductId = request.ProductId, ProductName = "OLD", DisplayName = "Old", Version = 6 });
        _repository.Setup(x => x.ProductNameExistsAsync("UPDATED", request.ProductId, default)).ReturnsAsync(false);
        _repository.Setup(x => x.UpdateProductAsync(product, default)).ReturnsAsync(updated);
        _mapper.Setup(x => x.Map<ProductResponse>(updated)).Returns(response);

        (await _service.UpdateProductAsync(request)).Should().BeSameAs(response);
        product.ProductName.Should().Be("UPDATED");
        _outbox.Verify(x => x.WriteAsync(It.Is<ProductOperationResultMessage>(m =>
            m.Operation == ProductOperation.Update && m.Status == ProductOperationStatus.Success && m.ProductVersion == 7), default), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldThrowAndNotWriteOutbox_WhenProductIsMissing()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid(), DisplayName = "Missing" };
        _mapper.Setup(x => x.Map<Product>(request)).Returns(new Product { ProductId = request.ProductId, DisplayName = request.DisplayName! });
        _repository.Setup(x => x.UpdateProductAsync(It.IsAny<Product>(), default)).ReturnsAsync((Product?)null);

        await FluentActions.Invoking(() => _service.UpdateProductAsync(request))
            .Should().ThrowAsync<ProductNotFoundException>();
        _outbox.Verify(x => x.WriteAsync(It.IsAny<ProductOperationResultMessage>(), default), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldThrowAndNotWriteOutbox_WhenVersionIsStale()
    {
        var request = new ProductUpdateRequest
        {
            ProductId = Guid.NewGuid(),
            DisplayName = "Updated",
            Version = 2
        };
        _mapper.Setup(x => x.Map<Product>(request)).Returns(new Product
        {
            ProductId = request.ProductId,
            DisplayName = request.DisplayName!,
            Version = request.Version
        });
        _repository.Setup(x => x.GetProductByProductIdAsync(request.ProductId)).ReturnsAsync(
            new Product { ProductId = request.ProductId, Version = 3 });

        await FluentActions.Invoking(() => _service.UpdateProductAsync(request))
            .Should().ThrowAsync<ProductConcurrencyException>();

        _repository.Verify(
            x => x.ProductNameExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), default),
            Times.Never);
        _outbox.Verify(x => x.WriteAsync(It.IsAny<ProductOperationResultMessage>(), default), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldThrowAndNotWriteOutbox_WhenNameExists()
    {
        var request = new ProductUpdateRequest
        {
            ProductId = Guid.NewGuid(),
            DisplayName = "Duplicate",
            Version = 2
        };
        _mapper.Setup(x => x.Map<Product>(request)).Returns(new Product
        {
            ProductId = request.ProductId,
            DisplayName = request.DisplayName!,
            Version = request.Version
        });
        _repository.Setup(x => x.GetProductByProductIdAsync(request.ProductId)).ReturnsAsync(
            new Product { ProductId = request.ProductId, Version = request.Version });
        _repository.Setup(x => x.ProductNameExistsAsync(
            "DUPLICATE", request.ProductId, default)).ReturnsAsync(true);

        await FluentActions.Invoking(() => _service.UpdateProductAsync(request))
            .Should().ThrowAsync<ProductAlreadyExistsException>();

        _repository.Verify(x => x.UpdateProductAsync(It.IsAny<Product>(), default), Times.Never);
        _outbox.Verify(x => x.WriteAsync(It.IsAny<ProductOperationResultMessage>(), default), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }
}
