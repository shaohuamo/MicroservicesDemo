using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace CommonService.Middlewares
{
    /// <summary>
    /// Resolve UserId from request header
    /// </summary>
    public class TraceContextMiddleware
    {
        public const string UserIdItemKey = "CommonService.TraceContext.UserId";

        private readonly RequestDelegate _next;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="next"></param>
        public TraceContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Middleware Logical 
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public async Task Invoke(HttpContext httpContext, ILogger<TraceContextMiddleware> logger)
        {
            // 010-000:Get UserId
            // Priority: HttpContext.User (current auth) > trusted gateway header > Baggage (upstream)
            var userId = GetClaimValue(httpContext.User, "sub")
                         ?? GetClaimValue(httpContext.User, ClaimTypes.NameIdentifier)
                         ?? GetClaimValue(httpContext.User, "nameidentifier")
                         ?? httpContext.Request.Headers["X-User-Id"].ToString();

            if (string.IsNullOrEmpty(userId))
            {
                userId = Baggage.GetBaggage("user_id");
            }

            userId = string.IsNullOrEmpty(userId) ? "anonymous" : userId;
            httpContext.Items[UserIdItemKey] = userId;

            // 020-000:set useId for distributed trace propagation
            Baggage.SetBaggage("user_id", userId);

            //set user_id tag to current activity for better traceability in observability tools(such as jaeger)
            var currentActivity = Activity.Current;
            currentActivity?.SetTag("user_id", userId);

            // 030-000:inject logContext
            var scopeData = new Dictionary<string, object>
            {
                ["UserId"] = userId
            };

            using (logger.BeginScope(scopeData))
            {
                await _next(httpContext);
            }
        }

        private static string? GetClaimValue(ClaimsPrincipal? user, string claimType)
        {
            return user?.FindFirst(claimType)?.Value;
        }
    }

    /// <summary>
    /// TraceContextMiddleware Helper class
    /// </summary>
    public static class TraceContextMiddlewareExtensions
    {
        /// <summary>
        /// Add TraceContextMiddleware to middleware pipeline
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IApplicationBuilder TraceContextMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TraceContextMiddleware>();
        }
    }
}
