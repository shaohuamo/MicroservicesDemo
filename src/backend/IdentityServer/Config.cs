using Duende.IdentityServer.Models;

namespace IdentityServer;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
        new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile
            {
                UserClaims = { "preferred_username" }
            },
            new IdentityResources.Email(),
        };

    public static IEnumerable<ApiScope> ApiScopes =>
        new ApiScope[]
        {
            new ApiScope("products-api", "Products API")
            {
                UserClaims = { "name", "preferred_username", "email", "email_verified" }
            },
            new ApiScope("notifications-api", "Notifications API")
            {
                UserClaims = { "name", "preferred_username", "email", "email_verified" }
            },
        };

    public static IEnumerable<ApiResource> ApiResources =>
        new ApiResource[]
        {
            new ApiResource("gateway-api", "API Gateway")
            {
                Scopes = { "products-api", "notifications-api" },
                UserClaims = { "name", "preferred_username", "email", "email_verified" }
            },
        };

    public static IEnumerable<Client> GetClients(IConfiguration configuration)
    {
        var frontendPublicUrl = configuration["Clients:Frontend:PublicUrl"] ?? "http://localhost:3000";
        var frontendClientId = configuration["Clients:Frontend:ClientId"] ?? "test_app";
        var frontendClientSecret = configuration["Clients:Frontend:ClientSecret"] ?? "frontend-secret";
        var postmanRedirectUri = configuration["Clients:Postman:RedirectUri"] ?? "https://oauth.pstmn.io/v1/callback";
        var tokenLifetimes = GetTokenLifetimes(configuration);

        return
        new Client[]
        {
            new Client
            {
                ClientId = "postman",
                ClientName = "Postman",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false,
                RedirectUris = { postmanRedirectUri },
                PostLogoutRedirectUris = { postmanRedirectUri },
                AllowedScopes = { "openid", "profile", "email", "products-api", "notifications-api" },
                AllowOfflineAccess = true,
                AccessTokenLifetime = tokenLifetimes.AccessTokenSeconds,
                AbsoluteRefreshTokenLifetime = tokenLifetimes.AbsoluteRefreshTokenSeconds,
            },

            new Client
            {
                ClientId = frontendClientId,
                ClientName = "Admin Frontend",
                ClientSecrets = { new Secret(frontendClientSecret.Sha256()) },
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RedirectUris = { $"{frontendPublicUrl}/api/auth/callback/identity-server" },
                PostLogoutRedirectUris =
                {
                    $"{frontendPublicUrl}/",
                    $"{frontendPublicUrl}/products"
                },
                AllowedCorsOrigins = { frontendPublicUrl },
                AlwaysIncludeUserClaimsInIdToken = true,
                AllowOfflineAccess = true,
                AllowedScopes = { "openid", "profile", "email", "products-api", "notifications-api" },
                AccessTokenLifetime = tokenLifetimes.AccessTokenSeconds,
                AbsoluteRefreshTokenLifetime = tokenLifetimes.AbsoluteRefreshTokenSeconds,
            },
        };
    }

    private static TokenLifetimeOptions GetTokenLifetimes(IConfiguration configuration)
    {
        var section = configuration.GetSection("IdentityServer:TokenLifetimes");

        return new TokenLifetimeOptions(
            AccessTokenSeconds: GetRequiredPositiveSeconds(section, "AccessTokenSeconds"),
            AbsoluteRefreshTokenSeconds: GetRequiredPositiveSeconds(section, "AbsoluteRefreshTokenSeconds"));
    }

    private static int GetRequiredPositiveSeconds(IConfiguration section, string key)
    {
        var value = section.GetValue<int?>(key);

        if (value is not > 0)
        {
            throw new InvalidOperationException(
                $"IdentityServer:TokenLifetimes:{key} must be configured with a positive number of seconds.");
        }

        return value.Value;
    }

    private sealed record TokenLifetimeOptions(
        int AccessTokenSeconds,
        int AbsoluteRefreshTokenSeconds);
}
