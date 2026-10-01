using CommonService.RabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductsMicroservice.Core.Messaging.OutboxWriterContracts;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Core.Services;
using ProductsMicroservice.Infrastructure.Extensions;
using ProductsMicroservice.Infrastructure.Decorators.Caching;
using ProductsMicroservice.Infrastructure.Decorators.Idempotency;
using ProductsMicroservice.Infrastructure.Decorators.Observability;
using ProductsMicroservice.Infrastructure.HostedServices;
using ProductsMicroservice.Infrastructure.Messaging;
using ProductsMicroservice.Infrastructure.Options;
using ProductsMicroservice.Infrastructure.Messaging.Outbox;

namespace ProductsMicroservice.Infrastructure
{
    public static class DependencyInjection
    {
        //Add ProductsMicroservice.Infrastructure Layer services into the IoC container
        public static IServiceCollection ProductsMicroserviceInfrastructure(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddScoped<ProductIdempotencyExecutor>();
            services.AddSingleton<ProductIdempotencyResultCache>();

            //decorate service
            services.Decorate<IProductsAdderService, ProductsAdderIdempotencyDecorator>();
            services.Decorate<IProductsAdderService, ProductsAdderCachingDecorator>();
            services.Decorate<IProductsAdderService, ProductsAdderTelemetryDecorator>();

            services.Decorate<IProductsDeleterService, ProductsDeleterIdempotencyDecorator>();
            services.Decorate<IProductsDeleterService, ProductsDeleterCachingDecorator>();
            services.Decorate<IProductsDeleterService, ProductsDeleterTelemetryDecorator>();

            services.Decorate<IProductsUpdaterService, ProductsUpdaterIdempotencyDecorator>();
            services.Decorate<IProductsUpdaterService, ProductsUpdaterCachingDecorator>();
            services.Decorate<IProductsUpdaterService, ProductsUpdaterTelemetryDecorator>();

            services.AddScoped<ProductsGetterService>();//to bypass decoraotr logic
            services.Decorate<IProductsGetterService, ProductsGetterCachingDecorator>();
            services.Decorate<IProductsGetterService, ProductsGetterTelemetryDecorator>();

            services.AddProductsDatabase(configuration);
            services.AddProductsRedis(configuration);

            services.Configure<RabbitMQOptions>(configuration.GetSection("RabbitMQ"));
            services.Configure<ProductOperationOutboxOptions>(
                configuration.GetSection(ProductOperationOutboxOptions.SectionName));
            services.Configure<ProductOperationMessagingOptions>(
                configuration.GetSection(ProductOperationMessagingOptions.SectionName));

            services.AddScoped<IProductOperationOutboxWriter, ProductOperationOutboxWriter>();

            services.AddSingleton<IProductOperationOutboxStore, ProductOperationOutboxStore>();
            services.AddSingleton<IRabbitMQConnectionProvider, RabbitMQConnectionProvider>();
            services.AddSingleton<ProductOperationRabbitMqPublisher>();
            services.AddHostedService<ProductOperationOutboxDispatcher>();
            services.AddHostedService<IdempotencyCleanupWorker>();

            services.AddHostedService<AppWarmupService>();

            return services;
        }
    }
}
