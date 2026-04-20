using CommonService.RabbitMQ;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.ExternalServices.Abstractions;
using ProductsMicroservice.Core.HttpClients;
using ProductsMicroservice.Core.MessageQueue.Abstractions;
using ProductsMicroservice.Core.Policies;
using ProductsMicroservice.Core.RabbitMQ;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Core.Services;
using ProductsMicroservice.Infrastructure.DbContext;
using ProductsMicroservice.Infrastructure.Decorators.Caching;
using ProductsMicroservice.Infrastructure.Decorators.Observability;
using ProductsMicroservice.Infrastructure.HostedServices;
using ProductsMicroservice.Infrastructure.Options;
using ProductsMicroservice.Infrastructure.Repositories;
using StackExchange.Redis;

namespace ProductsMicroservice.Infrastructure
{
    public static class DependencyInjection
    {
        //Add ProductsMicroservice.Infrastructure Layer services into the IoC container
        public static IServiceCollection ProductsMicroserviceInfrastructure(this IServiceCollection services, 
            IConfiguration configuration)
        {
            //decorate service
            services.Decorate<IProductsAdderService, ProductsAdderTelemetryDecorator>();

            services.Decorate<IProductsDeleterService, ProductsDeleterCachingDecorator>();
            services.Decorate<IProductsDeleterService, ProductsDeleterTelemetryDecorator>();

            services.Decorate<IProductsUpdaterService, ProductsUpdaterCachingDecorator>();
            services.Decorate<IProductsUpdaterService, ProductsUpdaterTelemetryDecorator>();

            services.AddScoped<ProductsGetterService>();//to bypass decoraotr logic
            services.Decorate<IProductsGetterService, ProductsGetterCachingDecorator>();
            services.Decorate<IProductsGetterService, ProductsGetterTelemetryDecorator>();

            // Register PostgresOptions
            services.Configure<PostgresOptions>(configuration.GetSection("POSTGRES"));

            services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            {
                var env = serviceProvider.GetRequiredService<IHostEnvironment>();
                var postgresOptions = serviceProvider.GetRequiredService<IOptions<PostgresOptions>>().Value;

                string connectionStringTemplate = configuration.GetConnectionString("PostgresConnection")!;
                string connectionString = connectionStringTemplate
                    .Replace("$POSTGRES_HOST", postgresOptions.Host)
                    .Replace("$POSTGRES_PASSWORD", postgresOptions.Password)
                    .Replace("$POSTGRES_DATABASE", postgresOptions.Database)
                    .Replace("$POSTGRES_PORT", postgresOptions.Port)
                    .Replace("$POSTGRES_USER", postgresOptions.User);

                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions .EnableRetryOnFailure(
                        maxRetryCount: postgresOptions.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(postgresOptions.MaxRetryDelaySeconds),
                        errorCodesToAdd: null
                    );
                });

                if (env.IsDevelopment())
                {
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                }
            });

            services.AddScoped<IProductsRepository, ProductsRepository>();
            services.AddSingleton<IProductMessagePublisher, RabbitMQProductAddProductAddPublisher>();

            services.AddHttpClient<ITestMicroserviceClient, TestMicroserviceClient>(client =>
            {
                client.BaseAddress = new Uri($"http://{configuration["TestMicroserviceName"]}:{configuration["TestMicroservicePort"]}");
            })
            //.AddPolicyHandler(
            //    services.BuildServiceProvider().GetRequiredService<ITestMicroservicePolicies>().GetCombinedPolicy())
            ;

            services.AddSingleton<ITestMicroservicePolicies, TestMicroservicePolicies>();

            services.AddSingleton<IRabbitMQConnectionProvider, RabbitMQConnectionProvider>();
            services.Configure<RabbitMQOptions>(configuration.GetSection("RabbitMQ"));

            var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

            var redisConfig = ConfigurationOptions.Parse(redisOptions.ConnectionString);
            redisConfig.ConnectRetry = redisOptions.ConnectRetry;
            redisConfig.ConnectTimeout = redisOptions.ConnectTimeout;
            redisConfig.SyncTimeout = redisOptions.SyncTimeout;
            redisConfig.AbortOnConnectFail = redisOptions.AbortOnConnectFail;
            redisConfig.ReconnectRetryPolicy = new ExponentialRetry(
                redisOptions.InitialReconnectDelay,
                redisOptions.MaxReconnectDelay
            );

            var connectionMultiplexer = ConnectionMultiplexer.Connect(redisConfig);
            services.AddSingleton<IConnectionMultiplexer>(connectionMultiplexer);
            services.AddStackExchangeRedisCache(options =>
            {
                options.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(connectionMultiplexer);
                options.InstanceName = redisOptions.InstanceName;
            });

            services.AddSingleton<IDistributedLockProvider>(_ =>
            {
                var database = connectionMultiplexer.GetDatabase();
                return new RedisDistributedSynchronizationProvider(database);
            });

            services.Configure<CacheOptions>(configuration.GetSection(RedisOptions.SectionName));

            services.AddHostedService<AppWarmupService>();
            return services;
        }
    }
}