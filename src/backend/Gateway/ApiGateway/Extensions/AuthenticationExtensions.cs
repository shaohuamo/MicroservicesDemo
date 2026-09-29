using Microsoft.AspNetCore.Authentication.JwtBearer;
using StackExchange.Redis;

namespace ApiGateway.Extensions;

/// <summary>
/// Registers authentication services for the API Gateway.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Adds JWT Bearer authentication and access-token denylist validation.
    /// </summary>
    public static IServiceCollection AddGatewayAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = configuration["Authentication:Authority"];
                options.Audience = configuration["Authentication:Audience"] ?? "gateway-api";
                options.RequireHttpsMetadata = configuration.GetValue(
                    "Authentication:RequireHttpsMetadata", false);
                options.MapInboundClaims = false;
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        string? jti = context.Principal?.FindFirst("jti")?.Value;

                        if (string.IsNullOrWhiteSpace(jti))
                        {
                            return;
                        }

                        var redis = context.HttpContext.RequestServices
                            .GetService<IConnectionMultiplexer>();
                        if (redis is null)
                        {
                            return;
                        }

                        try
                        {
                            string denylistPrefix = context.HttpContext.RequestServices
                                .GetRequiredService<IConfiguration>()["Authentication:AccessTokenDenylistPrefix"]
                                ?? "admin-web:access-token-denylist";
                            bool isDenied = await redis.GetDatabase()
                                .KeyExistsAsync($"{denylistPrefix}:{jti}");

                            if (isDenied)
                            {
                                context.Fail("Access token has been revoked.");
                            }
                        }
                        catch
                        {
                            bool failClosed = context.HttpContext.RequestServices
                                .GetRequiredService<IConfiguration>()
                                .GetValue("Authentication:DenylistFailClosed", true);
                            if (failClosed)
                            {
                                context.Fail("Access token denylist is unavailable.");
                            }
                        }
                    }
                };

                string? metadataAddress = configuration["Authentication:MetadataAddress"];
                if (!string.IsNullOrWhiteSpace(metadataAddress))
                {
                    options.MetadataAddress = metadataAddress;
                }
            });

        return services;
    }
}
