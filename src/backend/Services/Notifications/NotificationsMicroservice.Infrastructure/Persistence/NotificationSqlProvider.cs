using Microsoft.Extensions.Configuration;

namespace NotificationsMicroservice.Infrastructure.Persistence;

internal sealed class NotificationSqlProvider(IConfiguration configuration)
{
    private const string NotificationsSection = "SqlQueries:Notifications";

    public string Get(string sqlId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sqlId);

        var sql = configuration[$"{NotificationsSection}:{sqlId}"];
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new InvalidOperationException(
                $"SQL query '{sqlId}' was not found in configuration section '{NotificationsSection}'.");
        }

        return sql;
    }
}
