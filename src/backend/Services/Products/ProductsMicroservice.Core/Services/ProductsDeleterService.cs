using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.ExternalServices.Abstractions;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Core.Services;

public class ProductsDeleterService: IProductsDeleterService
{
    private readonly IProductsRepository _productsRepository;
    private readonly ITestMicroserviceClient _testMicroserviceClient;
    private readonly ILogger<ProductsDeleterService> _logger;

    public ProductsDeleterService(IProductsRepository productsRepository,
        ITestMicroserviceClient testMicroserviceClient, ILogger<ProductsDeleterService> logger)
    {
        _productsRepository = productsRepository;
        _testMicroserviceClient = testMicroserviceClient;
        _logger = logger;
    }

    public async Task<bool> DeleteProductAsync(Guid productId)
    {
        //010-000:delete product in database
        bool isDeleted = await _productsRepository.DeleteProductAsync(productId);

        if (!isDeleted)
        {
            _logger.LogWarning("Product deletion failed or product not found");
            return false;
        }

        _logger.LogInformation("Product successfully deleted from DB");

        // 020-000:call downstream service
        //invoke test microservice to delete related info of the deleted product
        bool isProductRelatedInfoDeleted = 
            await _testMicroserviceClient.DeleteProductRelatedInfoByProductIdAsync(productId);

        return isDeleted;
    }
}