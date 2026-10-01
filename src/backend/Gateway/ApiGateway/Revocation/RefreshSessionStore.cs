using Npgsql;

namespace ApiGateway.Revocation;

public interface IRefreshSessionStore
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}

public sealed class PostgresRefreshSessionStore(NpgsqlDataSource dataSource) : IRefreshSessionStore
{
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT EXISTS(SELECT 1 FROM public.auth_refresh_tokens WHERE id = @id)");
        command.Parameters.AddWithValue("id", id);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT id FROM public.auth_refresh_tokens LIMIT 0");
        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }
}
