using System.Diagnostics;
using System.Security.Claims;
using OpenTelemetry;

namespace ApiGateway.Middleware;

/// <summary>
/// Replaces caller-supplied identity headers with values from the validated JWT.
/// </summary>
public sealed class IdentityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Handles an HTTP request.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        RemoveUntrustedIdentityHeaders(context.Request.Headers);

        string? userId = GetClaimValue(context.User, "sub")
            ?? GetClaimValue(context.User, ClaimTypes.NameIdentifier)
            ?? GetClaimValue(context.User, "nameidentifier");
        string? email = GetClaimValue(context.User, "email")
            ?? GetClaimValue(context.User, ClaimTypes.Email);
        string? emailVerified = GetClaimValue(context.User, "email_verified");

        if (!string.IsNullOrEmpty(userId))
        {
            context.Request.Headers["X-User-Id"] = userId;
            Baggage.SetBaggage("user_id", userId);
            Activity.Current?.SetTag("user_id", userId);
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            context.Request.Headers["X-User-Email"] = email;
        }

        if (!string.IsNullOrWhiteSpace(emailVerified))
        {
            context.Request.Headers["X-User-Email-Verified"] = emailVerified;
        }

        await next(context);
    }

    private static void RemoveUntrustedIdentityHeaders(IHeaderDictionary headers)
    {
        string[] untrustedHeaderNames = headers.Keys
            .Where(headerName => headerName.StartsWith("X-User-", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (string headerName in untrustedHeaderNames)
        {
            headers.Remove(headerName);
        }
    }

    private static string? GetClaimValue(ClaimsPrincipal? user, string claimType) =>
        user?.FindFirst(claimType)?.Value;
}

/// <summary>
/// Middleware registration extensions.
/// </summary>
public static class IdentityHeadersMiddlewareExtensions
{
    /// <summary>
    /// Replaces externally supplied identity headers with validated JWT claims.
    /// </summary>
    public static IApplicationBuilder UseIdentityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<IdentityHeadersMiddleware>();
}
