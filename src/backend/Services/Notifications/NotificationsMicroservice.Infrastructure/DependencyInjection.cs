using CommonService.RabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationsMicroservice.Core.Abstractions;
using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Infrastructure.Delivery;
using NotificationsMicroservice.Infrastructure.Extensions;
using NotificationsMicroservice.Infrastructure.Health;
using NotificationsMicroservice.Infrastructure.HostedServices;
using NotificationsMicroservice.Infrastructure.Messaging;
using NotificationsMicroservice.Infrastructure.Options;
using NotificationsMicroservice.Infrastructure.Persistence;
using NotificationsMicroservice.Infrastructure.Repositories;
using Resend;

namespace NotificationsMicroservice.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsMicroserviceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));
        services.Configure<ProductOperationsOptions>(configuration.GetSection(ProductOperationsOptions.SectionName));
        services.Configure<DeliveryOptions>(configuration.GetSection(DeliveryOptions.SectionName));
        services.Configure<ResendEmailOptions>(configuration.GetSection(ResendEmailOptions.SectionName));
        services.Configure<RabbitMQOptions>(configuration.GetSection("RabbitMQ"));

        services.AddSingleton<IProductOperationConsumerHealthState, ProductOperationConsumerHealthState>();
        services.AddSingleton<NotificationDbConnectionFactory>();
        services.AddSingleton<INotificationDatabaseHealthProbe, NotificationDatabaseHealthProbe>();
        services.AddSingleton<NotificationSqlProvider>();
        services.AddSingleton<INotificationAddRepository, NotificationAddRepository>();
        services.AddSingleton<INotificationGetRepository, NotificationGetRepository>();
        services.AddSingleton<INotificationUpdateRepository, NotificationUpdateRepository>();
        services.AddSingleton<INotificationDeleteRepository, NotificationDeleteRepository>();
        services.AddSingleton<INotificationDatabaseVerifier, NotificationDatabaseVerifier>();
        services.AddSingleton<IRabbitMQConnectionProvider, RabbitMQConnectionProvider>();

        services.AddNotificationsRedis(configuration);

        services.AddResend(options =>
        {
            options.ApiToken = configuration[$"{ResendEmailOptions.SectionName}:ApiToken"] ?? string.Empty;
        });
        services.AddScoped<INotificationEmailSender, ResendNotificationEmailSender>();

        // Registration order is intentional: database connectivity is checked before
        // consumers and workers begin their normal processing loops.
        services.AddHostedService<NotificationDatabaseInitializer>();
        services.AddHostedService<ProductOperationConsumer>();
        services.AddHostedService<NotificationDeliveryWorker>();
        services.AddHostedService<NotificationCleanupWorker>();

        return services;
    }
}
