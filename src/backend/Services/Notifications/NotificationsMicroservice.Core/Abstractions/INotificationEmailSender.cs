using NotificationsMicroservice.Core.Domain;

namespace NotificationsMicroservice.Core.Abstractions;

public interface INotificationEmailSender
{
    Task<string> SendAsync(Notification notification, CancellationToken cancellationToken);
}
