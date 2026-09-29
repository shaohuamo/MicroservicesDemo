using ProductsMicroservice.Core.DTO;

namespace ProductsMicroservice.Core.ServiceContracts;

public interface IProductOperationContextAccessor
{
    ProductOperationContext GetCurrent();
}
