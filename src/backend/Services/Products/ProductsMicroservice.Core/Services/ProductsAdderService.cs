using AutoMapper;
using CommonService.Messages;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.Domain;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.Domain.Services;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Messaging;
using ProductsMicroservice.Core.Messaging.OutboxWriterContracts;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Core.Options;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProductsMicroservice.Core.Services;

public class ProductsAdderService : IProductsAdderService
{
    private readonly IMapper _mapper;
    private readonly IProductsRepository _productsRepository;
    private readonly IProductOperationOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductOperationContextAccessor _operationContextAccessor;
    private readonly IIdempotencyRepository _idempotencyRepository;
    private readonly IdempotencyOptions _idempotencyOptions;
    private readonly ILogger<ProductsAdderService> _logger;

    public ProductsAdderService(
        IMapper mapper,
        IProductsRepository productsRepository,
        IProductOperationOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork,
        IProductOperationContextAccessor operationContextAccessor,
        IIdempotencyRepository idempotencyRepository,
        IOptions<IdempotencyOptions> idempotencyOptions,
        ILogger<ProductsAdderService> logger)
    {
        _mapper = mapper;
        _productsRepository = productsRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
        _operationContextAccessor = operationContextAccessor;
        _idempotencyRepository = idempotencyRepository;
        _idempotencyOptions = idempotencyOptions.Value;
        _logger = logger;
    }

    public async Task<ProductAddResult> AddProductAsync(
        ProductAddRequest productAddRequest,
        Guid idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(productAddRequest);

        ProductOperationContext operationContext = _operationContextAccessor.GetCurrent();
        string requestHash = CreateRequestHash(productAddRequest);

        ProductAddResult? replay = await TryGetStoredIdempotencyResultAsync(
            operationContext.UserId, idempotencyKey, requestHash);
        if (replay is not null)
        {
            return replay;
        }

        Guid notificationId = Guid.CreateVersion7();
        Product productInput = _mapper.Map<Product>(productAddRequest);
        ProductNames names = ProductNameNormalizer.Normalize(productInput.DisplayName);
        productInput.DisplayName = names.DisplayName;
        productInput.ProductName = names.ProductName;
        if (productInput.ProductId == Guid.Empty)
        {
            productInput.ProductId = Guid.NewGuid();
        }

        _logger.LogInformation("Creating product {ProductId}: {DisplayName}",
            productInput.ProductId, productInput.DisplayName);

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
        DateTimeOffset now = DateTimeOffset.UtcNow;

        //store idempotency record
        _idempotencyRepository.Add(new IdempotencyRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = operationContext.UserId,
            Operation = IdempotencyOperation.AddProduct,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            ResponseStatusCode = 201,
            ResponseJson = JsonSerializer.Serialize(response, JsonSerializerOptions.Web),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(_idempotencyOptions.RetentionHours)
        });

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (IdempotencyRecordConflictException)
        {
            return await GetRequiredStoredIdempotencyResultAsync(
                operationContext.UserId, idempotencyKey, requestHash);
        }
        catch (ProductAlreadyExistsException)
        {
            ProductAddResult? concurrentReplay = await TryGetStoredIdempotencyResultAsync(
                operationContext.UserId, idempotencyKey, requestHash);
            if (concurrentReplay is not null)
            {
                return concurrentReplay;
            }

            throw;
        }

        _logger.LogInformation(
            "Product {ProductId} and notification {NotificationId} committed",
            addedProduct.ProductId, notificationId);

        return new ProductAddResult(response, false);
    }

    private async Task<ProductAddResult?> TryGetStoredIdempotencyResultAsync(
        string userId,
        Guid idempotencyKey,
        string requestHash)
    {
        IdempotencyRecord? existing = await _idempotencyRepository.GetAsync(
            userId, IdempotencyOperation.AddProduct, idempotencyKey);
        if (existing is null)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(existing.RequestHash),
                Convert.FromHexString(requestHash)))
        {
            throw new IdempotencyPayloadConflictException();
        }

        ProductResponse response = JsonSerializer.Deserialize<ProductResponse>(
            existing.ResponseJson, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Stored idempotency response is invalid.");
        return new ProductAddResult(response, true);
    }

    private async Task<ProductAddResult> GetRequiredStoredIdempotencyResultAsync(
        string userId,
        Guid idempotencyKey,
        string requestHash) =>
        await TryGetStoredIdempotencyResultAsync(userId, idempotencyKey, requestHash)
        ?? throw new InvalidOperationException("The concurrent idempotency record was not found.");

    private static string CreateRequestHash(ProductAddRequest request)
    {
        ProductNames names = ProductNameNormalizer.Normalize(request.DisplayName);
        string canonical = JsonSerializer.Serialize(new
        {
            names.DisplayName,
            UnitPrice = request.UnitPrice,
            QuantityInStock = request.QuantityInStock
        }, JsonSerializerOptions.Web);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
