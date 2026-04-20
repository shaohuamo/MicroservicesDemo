using AutoMapper;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Core.Services;

public class ProductsGetterService : IProductsGetterService
{
    private readonly IProductsRepository _productsRepository;
    private readonly IMapper _mapper;

    public ProductsGetterService(IProductsRepository productsRepository, IMapper mapper)
    {
        _productsRepository = productsRepository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<ProductResponse?>> GetProductsAsync()
    {
        var products = await _productsRepository.GetProductsAsync();

        //Invokes ProductToProductResponseMappingProfile
        return _mapper.Map<IEnumerable<ProductResponse>>(products);
    }

    public async Task<ProductResponse?> GetProductByProductIdAsync(Guid productId)
    {
        var product = await _productsRepository.GetProductByProductIdAsync(productId);
        if (product == null)
        {
            return null;
        }

        //Invokes ProductToProductResponseMappingProfile
        return _mapper.Map<ProductResponse>(product);
    }
}