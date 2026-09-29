using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NotificationsMicroservice.Infrastructure.Persistence;

namespace NotificationsMicroservice.Tests;

public sealed class NotificationSqlProviderTests
{
    [Fact]
    public void Get_WhenSqlExistsInXml_ReturnsFormattedSql()
    {
        var configuration = new ConfigurationBuilder()
            .AddXmlFile(GetSqlFilePath(), optional: false, reloadOnChange: false)
            .Build();
        var provider = new NotificationSqlProvider(configuration);

        var sql = provider.Get("Select_Notification_001");

        sql.Should().Contain("SELECT");
        sql.Should().Contain("\"PayloadHash\"");
        sql.Should().Contain("FROM");
        sql.Should().Contain("public.\"Notifications\"");
    }

    [Fact]
    public void Get_WhenSqlDoesNotExist_ThrowsClearException()
    {
        var provider = new NotificationSqlProvider(new ConfigurationBuilder().Build());

        var action = () => provider.Get("Select_Notification_999");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Select_Notification_999*");
    }

    private static string GetSqlFilePath() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "src", "backend", "Services", "Notifications",
        "NotificationsMicroservice.Infrastructure", "Sql", "Notifications.xml"));
}
