using CommonService.Middlewares;
using Microsoft.Extensions.Primitives;

namespace NotificationsMicroservice.API.Middleware;

/// <summary>
/// Requires the gateway-provided user ID on notification endpoints. Trace baggage is never an identity source.
/// </summary>
public sealed class GatewayUserIdMiddleware(RequestDelegate next)
{
    private const int MaxUserIdLength = 256;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/notifications"))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-User-Id", out StringValues values) ||
            values.Count != 1 ||
            string.IsNullOrWhiteSpace(values[0]) ||
            values[0]!.Length > MaxUserIdLength ||
            values[0] != values[0]!.Trim() ||
            values[0] == "anonymous" ||
            context.Items[TraceContextMiddleware.UserIdItemKey] is not string resolvedUserId ||
            !string.Equals(resolvedUserId, values[0], StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }
}
