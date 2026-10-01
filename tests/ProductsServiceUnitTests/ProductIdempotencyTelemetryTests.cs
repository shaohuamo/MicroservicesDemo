using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.Diagnostics;
using ProductsMicroservice.Core.Domain;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Observability;

namespace ProductsMicroservice.Tests;

public sealed class ProductIdempotencyTelemetryTests
{
    [Theory]
    [InlineData(IdempotencyOperation.AddProduct, IdempotencyResultSource.Redis)]
    [InlineData(IdempotencyOperation.AddProduct, IdempotencyResultSource.Database)]
    [InlineData(IdempotencyOperation.UpdateProduct, IdempotencyResultSource.Redis)]
    [InlineData(IdempotencyOperation.UpdateProduct, IdempotencyResultSource.Database)]
    [InlineData(IdempotencyOperation.DeleteProduct, IdempotencyResultSource.Redis)]
    [InlineData(IdempotencyOperation.DeleteProduct, IdempotencyResultSource.Database)]
    public async Task Replay_ShouldRecordSourceAndNeverChangeProductCount(IdempotencyOperation operation, IdempotencyResultSource source)
    {
        Guid productId = Guid.NewGuid();
        Guid key = Guid.NewGuid();
        int changes = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument == DiagnosticsConfig.ProductsCounter) meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<int>((_, measurement, tags, _) =>
        {
            foreach (var tag in tags)
                if (tag.Key == "product.id" && Equals(tag.Value, productId)) Interlocked.Add(ref changes, measurement);
        });
        listener.Start();
        using var activity = new Activity("test product operation").Start();
        var product = new ProductResponse(productId, "Product", 10, 2, 2);

        switch (operation)
        {
            case IdempotencyOperation.AddProduct:
                var adder = new Mock<IProductsAdderService>();
                adder.Setup(x => x.AddProductAsync(It.IsAny<ProductAddRequest>(), key))
                    .ReturnsAsync(new ProductAddResult(product, true) { Source = source });
                await new ProductsAdderTelemetryDecorator(adder.Object, Mock.Of<ILogger<ProductsAdderTelemetryDecorator>>())
                    .AddProductAsync(new ProductAddRequest { DisplayName = "Product" }, key);
                break;
            case IdempotencyOperation.UpdateProduct:
                var updater = new Mock<IProductsUpdaterService>();
                updater.Setup(x => x.UpdateProductAsync(It.IsAny<ProductUpdateRequest>(), key))
                    .ReturnsAsync(new ProductUpdateResult(product, true) { Source = source });
                await new ProductsUpdaterTelemetryDecorator(updater.Object, Mock.Of<ILogger<ProductsUpdaterTelemetryDecorator>>())
                    .UpdateProductAsync(new ProductUpdateRequest { ProductId = productId, DisplayName = "Product" }, key);
                activity.GetTagItem("product.updated").Should().Be(false);
                break;
            default:
                var deleter = new Mock<IProductsDeleterService>();
                deleter.Setup(x => x.DeleteProductAsync(productId, 1, key))
                    .ReturnsAsync(new ProductDeleteResult(true, true) { Source = source });
                await new ProductsDeleterTelemetryDecorator(deleter.Object, Mock.Of<ILogger<ProductsDeleterTelemetryDecorator>>())
                    .DeleteProductAsync(productId, 1, key);
                activity.GetTagItem("product.deleted").Should().Be(false);
                break;
        }

        changes.Should().Be(0);
        activity.GetTagItem("idempotency.source").Should().Be(source.ToString().ToLowerInvariant());
        activity.GetTagItem("idempotency.replayed").Should().Be(true);
        activity.GetTagItem("idempotency.outcome").Should().Be("replayed");
        activity.GetTagItem("idempotency.operation").Should().Be(operation.ToString());
    }
}
