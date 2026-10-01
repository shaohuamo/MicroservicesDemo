using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ApiGateway.Revocation;

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
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SessionProofVerifier>();
        services.AddSingleton<IRedisRevocationGuard, RedisRevocationGuard>();
        services.AddSingleton<AccessTokenRevocationValidator>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = configuration["Authentication:Authority"];
                options.Audience = configuration["Authentication:Audience"] ?? "gateway-api";
                options.RequireHttpsMetadata = configuration.GetValue(
                    "Authentication:RequireHttpsMetadata", false);
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var authorization = context.Request.Headers.Authorization.ToString();
                        var accessToken = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                            ? authorization[7..] : string.Empty;
                        var result = await context.HttpContext.RequestServices
                            .GetRequiredService<AccessTokenRevocationValidator>()
                            .ValidateAsync(context.HttpContext,
                                context.Principal?.FindFirst("jti")?.Value,
                                accessToken);
                        if (result == RevocationResult.Unavailable)
                            context.HttpContext.Items[AccessTokenRevocationValidator.UnavailableItem] = true;
                        if (result != RevocationResult.Allowed)
                            context.Fail(result == RevocationResult.Unavailable ? "Session store is unavailable." : "Access token revoked or session proof invalid.");
                    },
                    OnChallenge = context =>
                    {
                        if (context.HttpContext.Items.ContainsKey(AccessTokenRevocationValidator.UnavailableItem))
                        {
                            context.HandleResponse();
                            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        }
                        return Task.CompletedTask;
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
