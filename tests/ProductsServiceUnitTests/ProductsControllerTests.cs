using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroService.API.Controllers;

namespace ProductsMicroservice.Tests;

public class ProductsControllerTests
{
    private readonly Mock<IProductsGetterService> _getterMock = new();
    private readonly Mock<IProductsAdderService> _adderMock = new();
    private readonly Mock<IProductsDeleterService> _deleterMock = new();
    private readonly Mock<IProductsUpdaterService> _updaterMock = new();
    private readonly ProductsController _controller;

    public ProductsControllerTests()
    {
        _controller = new ProductsController(
            _updaterMock.Object,
            _getterMock.Object,
            _adderMock.Object,
            _deleterMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region Get Products

    [Fact]
    public async Task GetAllProductsAsync_ShouldReturnProducts()
    {
        List<ProductResponse?> products = [new ProductResponse(), null];
        _getterMock.Setup(x => x.GetProductsAsync()).ReturnsAsync(products);

        var result = await _controller.GetAllProductsAsync();

        result.Should().BeSameAs(products);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldReturnProduct_WhenFound()
    {
        var productId = Guid.NewGuid();
        var product = new ProductResponse(productId, "Product", 10, 2);
        _getterMock.Setup(x => x.GetProductByProductIdAsync(productId)).ReturnsAsync(product);

        var result = await _controller.GetProductByProductIdAsync(productId);

        result.Value.Should().BeSameAs(product);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldReturnNotFound_WhenMissing()
    {
        var productId = Guid.NewGuid();
        _getterMock.Setup(x => x.GetProductByProductIdAsync(productId))
            .ReturnsAsync((ProductResponse?)null);

        var result = await _controller.GetProductByProductIdAsync(productId);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Add Product

    private static readonly Guid IdempotencyKey = Guid.NewGuid();

    [Fact]
    public async Task AddNewProductAsync_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        var result = await _controller.AddNewProductAsync(null, IdempotencyKey.ToString("D"));

        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("The request body cannot be empty and must be a valid JSON.");
        _adderMock.Verify(x => x.AddProductAsync(
            It.IsAny<ProductAddRequest>(), It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("6f9619ff8b86d011b42d00c04fc964ff")]
    [InlineData("01992166-47b2-7f43-bc60-4750978888ac")]
    public async Task AddNewProductAsync_ShouldRejectMissingOrNonCanonicalKey(string? key)
    {
        Func<Task> action = () => _controller.AddNewProductAsync(new ProductAddRequest(), key);

        await action.Should().ThrowAsync<ProductsMicroservice.Core.Domain.Exceptions.IdempotencyKeyInvalidException>();
        _adderMock.Verify(x => x.AddProductAsync(
            It.IsAny<ProductAddRequest>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task AddNewProductAsync_ShouldPropagateServiceException()
    {
        var request = new ProductAddRequest();
        _adderMock.Setup(x => x.AddProductAsync(request, IdempotencyKey))
            .ThrowsAsync(new InvalidOperationException("save failed"));

        Func<Task> action = () => _controller.AddNewProductAsync(request, IdempotencyKey.ToString("D"));

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AddNewProductAsync_ShouldReturnCreatedAtAction_WhenSuccessful()
    {
        var request = new ProductAddRequest();
        var response = new ProductResponse(Guid.NewGuid(), "Product", 10, 2);
        _adderMock.Setup(x => x.AddProductAsync(request, IdempotencyKey))
            .ReturnsAsync(new ProductAddResult(response, false));

        var result = await _controller.AddNewProductAsync(request, IdempotencyKey.ToString("D"));

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(ProductsController.GetProductByProductIdAsync));
        created.RouteValues.Should().ContainKey("productId").WhoseValue.Should().Be(response.ProductId);
        created.Value.Should().BeSameAs(response);
        _controller.Response.Headers["Idempotency-Outcome"].ToString().Should().Be("created");
        _controller.Response.Headers["Idempotency-Replayed"].ToString().Should().Be("false");
    }

    [Fact]
    public async Task AddNewProductAsync_ShouldReturnSameCreatedResponse_WhenReplayed()
    {
        var request = new ProductAddRequest();
        var response = new ProductResponse(Guid.NewGuid(), "Product", 10, 2);
        _adderMock.Setup(x => x.AddProductAsync(request, IdempotencyKey))
            .ReturnsAsync(new ProductAddResult(response, true));

        var result = await _controller.AddNewProductAsync(request, IdempotencyKey.ToString("D"));

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.Value.Should().BeSameAs(response);
        created.RouteValues!["productId"].Should().Be(response.ProductId);
        _controller.Response.Headers["Idempotency-Outcome"].ToString().Should().Be("replayed");
        _controller.Response.Headers["Idempotency-Replayed"].ToString().Should().Be("true");
    }

    #endregion

    #region Update Product

    [Fact]
    public async Task UpdateProductAsync_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        var result = await _controller.UpdateProductAsync(null, IdempotencyKey.ToString("D"));

        result.Should().BeOfType<BadRequestObjectResult>();
        _updaterMock.Verify(x => x.UpdateProductAsync(It.IsAny<ProductUpdateRequest>(), IdempotencyKey), Times.Never);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldPropagateServiceException()
    {
        var request = new ProductUpdateRequest();
        _updaterMock.Setup(x => x.UpdateProductAsync(request, IdempotencyKey))
            .ThrowsAsync(new InvalidOperationException("save failed"));

        Func<Task> action = () => _controller.UpdateProductAsync(request, IdempotencyKey.ToString("D"));

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldReturnOk_WhenSuccessful()
    {
        var request = new ProductUpdateRequest();
        var response = new ProductResponse();
        _updaterMock.Setup(x => x.UpdateProductAsync(request, IdempotencyKey)).ReturnsAsync(new ProductUpdateResult(response, false));

        var result = await _controller.UpdateProductAsync(request, IdempotencyKey.ToString("D"));

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
    }

    #endregion

    #region Delete Product

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("6f9619ff8b86d011b42d00c04fc964ff")]
    [InlineData("01992166-47b2-7f43-bc60-4750978888ac")]
    public async Task UpdateAndDelete_ShouldRejectInvalidKeysWithoutCallingServices(string? key)
    {
        await FluentActions.Invoking(() => _controller.UpdateProductAsync(new ProductUpdateRequest(), key))
            .Should().ThrowAsync<ProductsMicroservice.Core.Domain.Exceptions.IdempotencyKeyInvalidException>();
        await FluentActions.Invoking(() => _controller.DeleteProductAsync(Guid.NewGuid(), new ProductDeleteRequest(), key))
            .Should().ThrowAsync<ProductsMicroservice.Core.Domain.Exceptions.IdempotencyKeyInvalidException>();
        _updaterMock.Verify(x => x.UpdateProductAsync(It.IsAny<ProductUpdateRequest>(), It.IsAny<Guid>()), Times.Never);
        _deleterMock.Verify(x => x.DeleteProductAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateAndDelete_ShouldPreserveBodiesAndExposeReplayHeaders(bool replay)
    {
        var request = new ProductUpdateRequest();
        var product = new ProductResponse(Guid.NewGuid(), "Original snapshot", 10, 2, 2);
        _updaterMock.Setup(x => x.UpdateProductAsync(request, IdempotencyKey))
            .ReturnsAsync(new ProductUpdateResult(product, replay) { Source = IdempotencyResultSource.Redis });
        _deleterMock.Setup(x => x.DeleteProductAsync(product.ProductId, 2, IdempotencyKey))
            .ReturnsAsync(new ProductDeleteResult(true, replay) { Source = IdempotencyResultSource.Redis });

        var updated = await _controller.UpdateProductAsync(request, IdempotencyKey.ToString("D"));
        updated.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(product);
        _controller.Response.Headers["Idempotency-Outcome"].ToString().Should().Be(replay ? "replayed" : "updated");
        _controller.Response.Headers["Idempotency-Replayed"].ToString().Should().Be(replay ? "true" : "false");
        var deleted = await _controller.DeleteProductAsync(product.ProductId, new ProductDeleteRequest { Version = 2 }, IdempotencyKey.ToString("D"));
        deleted.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(true);
        _controller.Response.Headers["Idempotency-Outcome"].ToString().Should().Be(replay ? "replayed" : "deleted");
        _controller.Response.Headers["Idempotency-Replayed"].ToString().Should().Be(replay ? "true" : "false");
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldReturnOk_WhenSuccessful()
    {
        var productId = Guid.NewGuid();
        var request = new ProductDeleteRequest { Version = 2 };
        _deleterMock.Setup(x => x.DeleteProductAsync(productId, request.Version, IdempotencyKey))
            .ReturnsAsync(new ProductDeleteResult(true, false));

        var result = await _controller.DeleteProductAsync(productId, request, IdempotencyKey.ToString("D"));

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(true);
        _deleterMock.Verify(x => x.DeleteProductAsync(productId, request.Version, IdempotencyKey), Times.Once);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldReturnBadRequest_WhenBodyIsMissing()
    {
        var result = await _controller.DeleteProductAsync(Guid.NewGuid(), null, IdempotencyKey.ToString("D"));

        result.Should().BeOfType<BadRequestObjectResult>();
        _deleterMock.Verify(
            x => x.DeleteProductAsync(It.IsAny<Guid>(), It.IsAny<int>(), IdempotencyKey),
            Times.Never);
    }

    #endregion
}
