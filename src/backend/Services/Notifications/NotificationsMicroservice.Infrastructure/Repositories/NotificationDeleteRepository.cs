using Dapper;
using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Infrastructure.Persistence;

namespace NotificationsMicroservice.Infrastructure.Repositories;

internal sealed class NotificationDeleteRepository(
    NotificationDbConnectionFactory connectionFactory,
    NotificationSqlProvider sqlProvider) : INotificationDeleteRepository
{
    /// <summary>
    /// Deletes successfully delivered notifications older than the retention cutoff.
    /// </summary>
    /// <returns>Returns the number of notification delivery rows deleted.</returns>
    public async Task<int> DeleteCompletedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        // Delete_Notification_001 removes completed notifications before the retention cutoff.
        return await connection.ExecuteAsync(new CommandDefinition(
            sqlProvider.Get("Delete_Notification_001"),
            new { CutoffUtc = cutoffUtc }, cancellationToken: cancellationToken));
    }
}
