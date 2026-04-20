using AutoMapper;
using Microsoft.Extensions.Configuration;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.MessageQueue.Abstractions;
using ProductsMicroservice.Core.MessageQueue.Messages;

namespace ProductsMicroservice.Core.Services;

public class ProductsAdderService: IProductsAdderService
{
    private readonly IMapper _mapper;
    private readonly IProductsRepository _productsRepository;
    private readonly IProductMessagePublisher _mqProductAddPublisher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProductsAdderService> _logger;

    public ProductsAdderService(
        IMapper mapper, IProductsRepository productsRepository, IProductMessagePublisher mqProductAddPublisher,
        IConfiguration configuration, ILogger<ProductsAdderService> logger)
    {
        _mapper = mapper;
        _productsRepository = productsRepository;
        _mqProductAddPublisher = mqProductAddPublisher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ProductResponse?> AddProductAsync(ProductAddRequest productAddRequest)
    {
        ArgumentNullException.ThrowIfNull(productAddRequest);//defend against null input

        _logger.LogInformation("Creating product: {ProductName}", productAddRequest.ProductName);

        //010-000:add product to database
        //Map productAddRequest into 'Product' type (it invokes ProductAddRequestToProductMappingProfile)
        Product productInput = _mapper.Map<Product>(productAddRequest);
        Product? addedProduct = await _productsRepository.AddProductAsync(productInput);

        if (addedProduct == null)
        {
            _logger.LogWarning("Product creation returned null from repository");
            return null;
        }

        //020-000: Publish message to MQ
        string routingKey = _configuration["RabbitMQ_Products_RoutingKey"]!;

        var productAddMessage = new ProductAddMessage(
            addedProduct.ProductId,
            addedProduct.ProductName,
            addedProduct.UnitPrice,
            addedProduct.QuantityInStock);

        _logger.LogInformation(
            "Publishing product created event to MQ with routing key {RoutingKey}",
            routingKey);

        await _mqProductAddPublisher.PublishAsync(routingKey, productAddMessage);

        _logger.LogInformation("Product created event published successfully");

        //Map addedProduct into 'ProductResponse' type (it invokes ProductToProductResponseMappingProfile)
        return _mapper.Map<ProductResponse>(addedProduct);
    }
}