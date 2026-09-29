using Dapper;
using NotificationsMicroservice.Core.Abstractions;

namespace NotificationsMicroservice.Infrastructure.Persistence;

internal sealed class NotificationDatabaseVerifier(NotificationDbConnectionFactory connectionFactory) : INotificationDatabaseVerifier
{
    public async Task VerifyConnectionAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            ALTER TABLE IF EXISTS public."Notifications"
                ADD COLUMN IF NOT EXISTS "TraceParent" character varying(512),
                ADD COLUMN IF NOT EXISTS "TraceState" character varying(512);
            """,
            cancellationToken: cancellationToken));
    }
}
