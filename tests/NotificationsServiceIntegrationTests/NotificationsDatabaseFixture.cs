using System.Xml.Linq;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using NotificationsMicroservice.Infrastructure.Options;
using NotificationsMicroservice.Infrastructure.Persistence;
using NotificationsMicroservice.Infrastructure.Repositories;
using Testcontainers.PostgreSql;

namespace NotificationsServiceIntegrationTests;

public sealed class NotificationsDatabaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _adminConnectionString;
    private string? _createdDatabase;
    internal NotificationDbConnectionFactory Factory { get; private set; } = null!;
    internal NotificationSqlProvider Sql { get; private set; } = null!;
    internal NotificationGetRepository GetRepository => new(Factory, Sql);
    internal NotificationUpdateRepository UpdateRepository => new(Factory, Sql);
    internal static string ProjectRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("NOTIFICATIONS_TEST_POSTGRES_CONNECTION_STRING");
        string connectionString;
        if (string.IsNullOrWhiteSpace(configured))
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _container.StartAsync();
            connectionString = _container.GetConnectionString();
        }
        else
        {
            _adminConnectionString = configured;
            var database = "notifications_replay_test_" + Guid.NewGuid().ToString("N");
            await using var admin = new NpgsqlConnection(configured);
            await admin.OpenAsync();
            await admin.ExecuteAsync($"CREATE DATABASE \"{database}\"");
            _createdDatabase = database;
            connectionString = new NpgsqlConnectionStringBuilder(configured) { Database = database }.ConnectionString;
        }
        var sqlDocument = XDocument.Load(Path.Combine(ProjectRoot, "src", "backend", "Services", "Notifications", "NotificationsMicroservice.Infrastructure", "Sql", "Notifications.xml"));
        var settings = sqlDocument.Root!.Element("SqlQueries")!.Element("Notifications")!.Elements()
            .ToDictionary(element => $"SqlQueries:Notifications:{element.Name.LocalName}", element => (string?)element.Value);
        settings["ConnectionStrings:PostgresConnection"] = connectionString;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        Factory = new NotificationDbConnectionFactory(configuration, Options.Create(new PostgresOptions()));
        Sql = new NotificationSqlProvider(configuration);
        await InitializeSchemaAsync();
    }

    internal async Task InitializeSchemaAsync()
    {
        await using var connection = await Factory.OpenConnectionAsync(default);
        var schema = await File.ReadAllTextAsync(Path.Combine(ProjectRoot, "configs", "postgres", "init", "create-notifications-table-and-indexes.sql"));
        schema = string.Join(Environment.NewLine, schema.Split('\n').Where(line => !line.StartsWith("\\c ")));
        await connection.ExecuteAsync(schema);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        if (_createdDatabase is not null)
        {
            await using var admin = new NpgsqlConnection(_adminConnectionString);
            await admin.OpenAsync();
            await admin.ExecuteAsync($"DROP DATABASE \"{_createdDatabase}\" WITH (FORCE)");
        }
        if (_container is not null) await _container.DisposeAsync();
    }
}
