using FluentAssertions;
using IdentityServer;
using Microsoft.Extensions.Configuration;

namespace IdentityServerUnitTests;

public sealed class ConfigTests
{
    [Fact]
    public void ApiConfiguration_ExposesNotificationScopeAndEmailClaims()
    {
        var notificationsScope = Config.ApiScopes.Single(scope => scope.Name == "notifications-api");
        var gatewayResource = Config.ApiResources.Single(resource => resource.Name == "gateway-api");

        notificationsScope.UserClaims.Should().Contain(["email", "email_verified"]);
        gatewayResource.Scopes.Should().Contain(["products-api", "notifications-api"]);
        gatewayResource.UserClaims.Should().Contain(["email", "email_verified"]);
    }

    [Fact]
    public void Clients_RequestNotificationAndEmailScopes()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IdentityServer:TokenLifetimes:AccessTokenSeconds"] = "900",
                ["IdentityServer:TokenLifetimes:AbsoluteRefreshTokenSeconds"] = "2592000",
            })
            .Build();

        var clients = Config.GetClients(configuration).ToArray();

        clients.Should().AllSatisfy(client =>
        {
            client.AllowedScopes.Should().Contain("email");
            client.AllowedScopes.Should().Contain("notifications-api");
        });
    }
}
