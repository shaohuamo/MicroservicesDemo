using CommonService.Messages;
using Microsoft.Extensions.Logging;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductOperationContextAccessor _operationContextAccessor;
    private readonly ILogger<ProductsDeleterService> _logger;

    public ProductsDeleterService(
        IProductsRepository productsRepository,
        IProductOperationOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork,
        IProductOperationContextAccessor operationContextAccessor,
        ILogger<ProductsDeleterService> logger)
    {
        _productsRepository = productsRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
        _operationContextAccessor = operationContextAccessor;
        _logger = logger;
    }

    public async Task DeleteProductAsync(Guid productId, int expectedVersion)
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
        await _unitOfWork.SaveChangesAsync();
    }
}
