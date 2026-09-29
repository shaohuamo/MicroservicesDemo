using AutoMapper;
using CommonService.Messages;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ProductsMicroservice.Core.Domain;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Messaging.OutboxWriterContracts;
using ProductsMicroservice.Core.Options;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Core.Services;

namespace ProductsMicroservice.Tests;

public class ProductsAdderServiceTests
{
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IProductsRepository> _repository = new();
    private readonly Mock<IProductOperationOutboxWriter> _outbox = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IIdempotencyRepository> _idempotencyRepository = new();
    private readonly ProductsAdderService _service;
    private readonly Guid _idempotencyKey = Guid.NewGuid();

    public ProductsAdderServiceTests()
    {
        var context = new Mock<IProductOperationContextAccessor>();
        context.Setup(x => x.GetCurrent()).Returns(new ProductOperationContext("user-1", "user@example.com", "en", "correlation-1"));
        _idempotencyRepository.Setup(x => x.GetAsync(
                It.IsAny<string>(), It.IsAny<IdempotencyOperation>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyRecord?)null);
        _service = new ProductsAdderService(_mapper.Object, _repository.Object, _outbox.Object,
            _unitOfWork.Object, context.Object, _idempotencyRepository.Object,
            Options.Create(new IdempotencyOptions()),
            Mock.Of<ILogger<ProductsAdderService>>());
    }

    [Fact]
    public async Task AddProductAsync_ShouldThrow_WhenRequestIsNull() =>
        await FluentActions.Invoking(() => _service.AddProductAsync(null!, _idempotencyKey))
            .Should().ThrowAsync<ArgumentNullException>();

    [Fact]
    public async Task AddProductAsync_ShouldCommitProductAndSuccessOutboxTogether()
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
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
        _idempotencyRepository.Verify(x => x.Add(It.Is<IdempotencyRecord>(record =>
            record.UserId == "user-1" &&
            record.Operation == IdempotencyOperation.AddProduct &&
            record.IdempotencyKey == _idempotencyKey &&
            record.ResponseStatusCode == 201 &&
            record.ExpiresAtUtc - record.CreatedAtUtc == TimeSpan.FromHours(24))), Times.Once);
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
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Never);
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
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task AddProductAsync_ShouldReplayStoredResponse_WithoutWritingAgain()
    {
        var request = new ProductAddRequest { DisplayName = " Test ", UnitPrice = 10, QuantityInStock = 2 };
        var expected = new ProductResponse(Guid.NewGuid(), "Test", 10, 2);
        IdempotencyRecord? captured = null;
        _idempotencyRepository.Setup(x => x.Add(It.IsAny<IdempotencyRecord>()))
            .Callback<IdempotencyRecord>(record => captured = record);
        SetupSuccessfulCreate(request, expected);

        await _service.AddProductAsync(request, _idempotencyKey);
        captured.Should().NotBeNull();
        _idempotencyRepository.Setup(x => x.GetAsync(
                "user-1", IdempotencyOperation.AddProduct, _idempotencyKey, default))
            .ReturnsAsync(captured);

        ProductAddResult replay = await _service.AddProductAsync(request, _idempotencyKey);

        replay.IsReplay.Should().BeTrue();
        replay.Product.Should().BeEquivalentTo(expected);
        _repository.Verify(x => x.AddProductAsync(It.IsAny<Product>(), default), Times.Once);
        _outbox.Verify(x => x.WriteAsync(It.IsAny<ProductOperationResultMessage>(), default), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task AddProductAsync_ShouldRejectDifferentPayload_ForSameKey()
    {
        var first = new ProductAddRequest { DisplayName = "Test", UnitPrice = 10, QuantityInStock = 2 };
        var response = new ProductResponse(Guid.NewGuid(), "Test", 10, 2);
        IdempotencyRecord? captured = null;
        _idempotencyRepository.Setup(x => x.Add(It.IsAny<IdempotencyRecord>()))
            .Callback<IdempotencyRecord>(record => captured = record);
        SetupSuccessfulCreate(first, response);
        await _service.AddProductAsync(first, _idempotencyKey);
        _idempotencyRepository.Setup(x => x.GetAsync(
                "user-1", IdempotencyOperation.AddProduct, _idempotencyKey, default))
            .ReturnsAsync(captured);

        var changed = new ProductAddRequest { DisplayName = "Test", UnitPrice = 11, QuantityInStock = 2 };
        await FluentActions.Invoking(() => _service.AddProductAsync(changed, _idempotencyKey))
            .Should().ThrowAsync<IdempotencyPayloadConflictException>();

        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    private void SetupSuccessfulCreate(ProductAddRequest request, ProductResponse response)
    {
        var product = new Product
        {
            ProductId = response.ProductId,
            DisplayName = request.DisplayName!,
            ProductName = "TEST",
            UnitPrice = request.UnitPrice!.Value,
            QuantityInStock = request.QuantityInStock!.Value
        };
        _mapper.Setup(x => x.Map<Product>(request)).Returns(product);
        _repository.Setup(x => x.ProductNameExistsAsync("TEST", null, default)).ReturnsAsync(false);
        _repository.Setup(x => x.AddProductAsync(product, default)).ReturnsAsync(product);
        _mapper.Setup(x => x.Map<ProductResponse>(product)).Returns(response);
    }
}
