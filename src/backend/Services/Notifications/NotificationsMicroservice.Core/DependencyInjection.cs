using Microsoft.Extensions.DependencyInjection;
using NotificationsMicroservice.Core.ServiceContracts;
using NotificationsMicroservice.Core.Services;

namespace NotificationsMicroservice.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsMicroserviceCore(this IServiceCollection services)
    {
        services.AddScoped<INotificationsGetterService, NotificationsGetterService>();
        services.AddScoped<INotificationsUpdaterService, NotificationsUpdaterService>();
        return services;
    }
}
