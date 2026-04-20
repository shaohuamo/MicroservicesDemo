using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductsMicroservice.Core.ExternalServices.Abstractions
{
    public interface ITestMicroserviceClient
    {
        Task<bool> DeleteProductRelatedInfoByProductIdAsync(Guid productId);
    }
}
