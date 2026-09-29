using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NotificationsMicroservice.Infrastructure.Options;
using Npgsql;

namespace NotificationsMicroservice.Infrastructure.Persistence;

public sealed class NotificationDbConnectionFactory : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public NotificationDbConnectionFactory(
        IConfiguration configuration,
        IOptions<PostgresOptions> postgresOptions)
    {
        var options = postgresOptions.Value;
        var template = configuration.GetConnectionString("PostgresConnection")
            ?? "Host=$POSTGRES_HOST;Port=$POSTGRES_PORT;Database=$POSTGRES_DATABASE;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD";

        var connectionString = template
            .Replace("$POSTGRES_HOST", options.Host, StringComparison.Ordinal)
            .Replace("$POSTGRES_PORT", options.Port, StringComparison.Ordinal)
            .Replace("$POSTGRES_DATABASE", options.Database, StringComparison.Ordinal)
            .Replace("$POSTGRES_USER", options.User, StringComparison.Ordinal)
            .Replace("$POSTGRES_PASSWORD", options.Password, StringComparison.Ordinal);

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _dataSource = builder.Build();
    }

    public ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken) =>
        _dataSource.OpenConnectionAsync(cancellationToken);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

}
