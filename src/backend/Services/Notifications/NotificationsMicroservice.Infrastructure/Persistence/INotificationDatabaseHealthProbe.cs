namespace NotificationsMicroservice.Infrastructure.Persistence;

public interface INotificationDatabaseHealthProbe
{
    Task CheckAsync(CancellationToken cancellationToken);
}

public sealed class NotificationDatabaseHealthProbe(NotificationDbConnectionFactory connectionFactory)
    : INotificationDatabaseHealthProbe
{
    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync(cancellationToken);
    }
}
