namespace NotificationsMicroservice.Core.Abstractions;

public interface INotificationDatabaseVerifier
{
    Task VerifyConnectionAsync(CancellationToken cancellationToken);
}
