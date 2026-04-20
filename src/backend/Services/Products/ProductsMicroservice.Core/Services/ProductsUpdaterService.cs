using AutoMapper;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Core.Services;

public class ProductsUpdaterService: IProductsUpdaterService
{
    private readonly IMapper _mapper;
    private readonly IProductsRepository _productsRepository;
    private readonly ILogger<ProductsUpdaterService> _logger;

    public ProductsUpdaterService(IProductsRepository productsRepository, IMapper mapper, 
        ILogger<ProductsUpdaterService> logger)
    {
        _productsRepository = productsRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ProductResponse?> UpdateProductAsync(ProductUpdateRequest productUpdateRequest)
    {
        ArgumentNullException.ThrowIfNull(productUpdateRequest);//defend against null input

        Product product = _mapper.Map<Product>(productUpdateRequest);

        //update the product
        Product? updatedProduct = await _productsRepository.UpdateProductAsync(product);

        if (updatedProduct == null)
        {
            return null;
        }

        return _mapper.Map<ProductResponse>(updatedProduct);
    }
}