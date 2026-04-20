using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductsMicroservice.Core.Mappers;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Core.Services;

namespace ProductsMicroservice.Core
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddProductsMicroserviceCore(this IServiceCollection services, IConfiguration configuration)
        {
            //Add service into Ioc Container
            services.AddScoped<IProductsAdderService, ProductsAdderService>();
            services.AddScoped<IProductsDeleterService, ProductsDeleterService>();
            services.AddScoped<IProductsGetterService, ProductsGetterService>();
            services.AddScoped<IProductsUpdaterService, ProductsUpdaterService>();

            services.AddAutoMapper(
                cfg => { }, 
                typeof(ProductToProductResponseMappingProfile)
            );

            return services;
        }
    }
}
