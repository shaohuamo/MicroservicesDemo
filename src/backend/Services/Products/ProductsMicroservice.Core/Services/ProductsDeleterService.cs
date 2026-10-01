using CommonService.Messages;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.Messaging;
using ProductsMicroservice.Core.Messaging.OutboxWriterContracts;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Core.Services;

public class ProductsDeleterService : IProductsDeleterService
{
    private readonly IProductsRepository _productsRepository;
    private readonly IProductOperationOutboxWriter _outboxWriter;
    private readonly IProductOperationContextAccessor _operationContextAccessor;

    public ProductsDeleterService(
        IProductsRepository productsRepository,
        IProductOperationOutboxWriter outboxWriter,
        IProductOperationContextAccessor operationContextAccessor)
    {
        _productsRepository = productsRepository;
        _outboxWriter = outboxWriter;
        _operationContextAccessor = operationContextAccessor;
    }

    public async Task<ProductDeleteResult> DeleteProductAsync(Guid productId, int expectedVersion, Guid idempotencyKey)
    {
        Guid notificationId = Guid.CreateVersion7();
        var operationContext = _operationContextAccessor.GetCurrent();

        var deletedProduct = await _productsRepository.DeleteProductAsync(productId, expectedVersion);
        if (deletedProduct is null)
        {
            throw new ProductNotFoundException(productId);
        }

        ProductOperationResultMessage message = ProductOperationResultMessageFactory.Create(
            notificationId, operationContext, ProductOperation.Delete,
            ProductOperationStatus.Success, productId, deletedProduct.DisplayName,
            deletedProduct.Version);

        await _outboxWriter.WriteAsync(message);
        return new ProductDeleteResult(true, false);
    }
}
