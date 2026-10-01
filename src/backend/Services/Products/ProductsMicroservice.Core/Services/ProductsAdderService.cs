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

public class ProductsAdderService : IProductsAdderService
{
    private readonly IMapper _mapper;
    private readonly IProductsRepository _productsRepository;
    private readonly IProductOperationOutboxWriter _outboxWriter;
    private readonly IProductOperationContextAccessor _operationContextAccessor;

    public ProductsAdderService(
        IMapper mapper,
        IProductsRepository productsRepository,
        IProductOperationOutboxWriter outboxWriter,
        IProductOperationContextAccessor operationContextAccessor)
    {
        _mapper = mapper;
        _productsRepository = productsRepository;
        _outboxWriter = outboxWriter;
        _operationContextAccessor = operationContextAccessor;
    }

    public async Task<ProductAddResult> AddProductAsync(
        ProductAddRequest productAddRequest,
        Guid idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(productAddRequest);

        ProductOperationContext operationContext = _operationContextAccessor.GetCurrent();

        Guid notificationId = Guid.CreateVersion7();
        Product productInput = _mapper.Map<Product>(productAddRequest);
        ProductNames names = ProductNameNormalizer.Normalize(productInput.DisplayName);
        productInput.DisplayName = names.DisplayName;
        productInput.ProductName = names.ProductName;
        if (productInput.ProductId == Guid.Empty)
        {
            productInput.ProductId = Guid.NewGuid();
        }

        if (await _productsRepository.ProductNameExistsAsync(
                productInput.ProductName))
        {
            throw new ProductAlreadyExistsException(productInput.DisplayName);
        }

        //add product
        Product addedProduct = await _productsRepository.AddProductAsync(
            productInput);
        ProductOperationResultMessage message = ProductOperationResultMessageFactory.Create(
            notificationId, operationContext, ProductOperation.Add, ProductOperationStatus.Success,
            addedProduct.ProductId, addedProduct.DisplayName, addedProduct.Version);

        //write message to outbox
        await _outboxWriter.WriteAsync(message);

        ProductResponse response = _mapper.Map<ProductResponse>(addedProduct);
        return new ProductAddResult(response, false);
    }
}
