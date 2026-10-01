using AutoMapper;
using CommonService.Messages;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.Domain.Services;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Messaging;
using ProductsMicroservice.Core.Messaging.OutboxWriterContracts;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Core.Services;

public class ProductsUpdaterService : IProductsUpdaterService
{
    private readonly IMapper _mapper;
    private readonly IProductsRepository _productsRepository;
    private readonly IProductOperationOutboxWriter _outboxWriter;
    private readonly IProductOperationContextAccessor _operationContextAccessor;

    public ProductsUpdaterService(
        IProductsRepository productsRepository,
        IMapper mapper,
        IProductOperationOutboxWriter outboxWriter,
        IProductOperationContextAccessor operationContextAccessor)
    {
        _productsRepository = productsRepository;
        _mapper = mapper;
        _outboxWriter = outboxWriter;
        _operationContextAccessor = operationContextAccessor;
    }

    public async Task<ProductUpdateResult> UpdateProductAsync(ProductUpdateRequest productUpdateRequest, Guid idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(productUpdateRequest);

        Guid notificationId = Guid.CreateVersion7();
        ProductOperationContext operationContext = _operationContextAccessor.GetCurrent();
        Product product = _mapper.Map<Product>(productUpdateRequest);
        ProductNames names = ProductNameNormalizer.Normalize(product.DisplayName);
        product.DisplayName = names.DisplayName;
        product.ProductName = names.ProductName;

        Product? currentProduct = await _productsRepository.GetProductByProductIdAsync(product.ProductId);
        if (currentProduct is null)
        {
            throw new ProductNotFoundException(product.ProductId);
        }

        if (currentProduct.Version != product.Version)
        {
            throw new ProductConcurrencyException(product.ProductId);
        }

        if (await _productsRepository.ProductNameExistsAsync(
                product.ProductName, product.ProductId))
        {
            throw new ProductAlreadyExistsException(product.DisplayName);
        }

        Product? updatedProduct = await _productsRepository.UpdateProductAsync(product);
        if (updatedProduct is null)
        {
            throw new ProductNotFoundException(product.ProductId);
        }

        ProductOperationResultMessage message = ProductOperationResultMessageFactory.Create(
            notificationId, operationContext, ProductOperation.Update,
            ProductOperationStatus.Success, updatedProduct.ProductId,
            updatedProduct.DisplayName, updatedProduct.Version);

        await _outboxWriter.WriteAsync(message);

        return new ProductUpdateResult(_mapper.Map<ProductResponse>(updatedProduct), false);
    }
}
