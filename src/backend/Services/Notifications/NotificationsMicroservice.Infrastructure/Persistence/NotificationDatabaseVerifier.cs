using Dapper;
using NotificationsMicroservice.Core.Abstractions;

namespace NotificationsMicroservice.Infrastructure.Persistence;

internal sealed class NotificationDatabaseVerifier(NotificationDbConnectionFactory connectionFactory) : INotificationDatabaseVerifier
{
    public async Task VerifyConnectionAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT 1;",
            cancellationToken: cancellationToken));
    }
}
