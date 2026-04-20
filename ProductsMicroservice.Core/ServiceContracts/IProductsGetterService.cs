using ProductsMicroservice.Core.DTO;
using System.Linq.Expressions;
using ProductsMicroservice.Core.Domain.Entities;

namespace ProductsMicroservice.Core.ServiceContracts
{
    public interface IProductsGetterService
    {
        /// <summary>
        /// Retrieves products from the products repository
        /// </summary>
        /// <returns>Returns list of ProductResponse objects</returns>
        Task<IEnumerable<ProductResponse?>> GetProductsAsync();


        /// <summary>
        /// Returns a single product that matches with given condition
        /// </summary>
        /// <param name="productId"></param>
        /// <returns>Returns matching product or null</returns>
        Task<ProductResponse?> GetProductByProductIdAsync(Guid productId);
    }
}
