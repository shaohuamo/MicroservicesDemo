using System.Text.Json;
using FluentAssertions;

namespace ApiGatewayUnitTests;

public class NotificationRouteAuthenticationTests
{
    [Theory]
    [InlineData("ocelot.json")]
    [InlineData("ocelot.k8s.json")]
    public void NotificationRoutes_RequireBearerAndNotificationsScope(string fileName)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName,
                   "src", "backend", "Gateway", "ApiGateway", fileName)))
        {
            root = root.Parent;
        }

        root.Should().NotBeNull();
        var path = Path.Combine(root!.FullName, "src", "backend", "Gateway", "ApiGateway", fileName);
        using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        var routes = document.RootElement.GetProperty("Routes").EnumerateArray()
            .Where(route => route.GetProperty("UpstreamPathTemplate").GetString()!
                .StartsWith("/gateway/notifications", StringComparison.Ordinal))
            .ToArray();

        routes.Should().HaveCount(2);
        foreach (var route in routes)
        {
            var authentication = route.GetProperty("AuthenticationOptions");
            authentication.GetProperty("AuthenticationProviderKey").GetString().Should().Be("Bearer");
            authentication.GetProperty("AllowedScopes").EnumerateArray()
                .Select(scope => scope.GetString()).Should().Contain("notifications-api");
        }
    }
}
