using System.Diagnostics;

using CommonService.Middlewares;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ProductsMicroservice.Core.Domain.Exceptions;

namespace ProductsMicroService.API.Middleware
{
    /// <summary>
    /// Custom Exception Handle Middleware
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="next">next middleware</param>
        /// <param name="logger"></param>
        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }


        /// <summary>
        /// Middleware Logical 
        /// </summary>
        /// <param name="httpContext"></param>
        public async Task Invoke(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                var userId = httpContext.Items.TryGetValue(TraceContextMiddleware.UserIdItemKey, out var value)
                    && value is string resolvedUserId
                    && !string.IsNullOrWhiteSpace(resolvedUserId)
                        ? resolvedUserId
                        : "anonymous";

                var (status, title, errorCode, detail) = ex switch
                {
                    UnauthorizedAccessException => (
                        StatusCodes.Status401Unauthorized,
                        "Unauthorized",
                        "authentication.required",
                        "Authentication is required to access this resource."),
                    ProductAlreadyExistsException => (
                        StatusCodes.Status409Conflict,
                        "Product already exists",
                        "product.already_exists",
                        "A product with the same name already exists."),
                    IdempotencyKeyInvalidException => (
                        StatusCodes.Status400BadRequest,
                        "Invalid idempotency key",
                        "idempotency.key_invalid",
                        "Idempotency-Key must be a UUID v4 in canonical format."),
                    IdempotencyPayloadConflictException => (
                        StatusCodes.Status409Conflict,
                        "Idempotency payload conflict",
                        "idempotency.payload_conflict",
                        "The Idempotency-Key was already used with a different request payload."),
                    ProductNotFoundException => (
                        StatusCodes.Status404NotFound,
                        "Product not found",
                        "product.not_found",
                        "The requested product could not be found."),
                    ProductConcurrencyException or DbUpdateConcurrencyException => (
                        StatusCodes.Status409Conflict,
                        "Product concurrency conflict",
                        "product.concurrency_conflict",
                        "The product was modified or deleted by another request. Refresh it and try again."),
                    RetryLimitExceededException => (
                        StatusCodes.Status503ServiceUnavailable,
                        "Database unavailable",
                        "product.database_unavailable",
                        "The product database is temporarily unavailable. Try again later."),
                    DbUpdateException => (
                        StatusCodes.Status500InternalServerError,
                        "Product persistence failed",
                        "product.persistence_failed",
                        "The product change could not be saved."),
                    _ => (
                        StatusCodes.Status500InternalServerError,
                        "Internal Server Error",
                        "internal_error",
                        "An unexpected error occurred.")
                };

                using (_logger.BeginScope(new Dictionary<string, object>
                {
                    ["UserId"] = userId
                }))
                {
                    if (status >= StatusCodes.Status500InternalServerError)
                    {
                        _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
                    }
                    else
                    {
                        _logger.LogWarning(ex, "Request failed: {Message}", ex.Message);
                    }
                }

                var activity = Activity.Current;
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);

                var response = new ProblemDetails
                {
                    Status = status,
                    Title = title,
                    Detail = detail,
                    Instance = httpContext.Request.Path
                };
                response.Extensions["errorCode"] = errorCode;
                response.Extensions["traceId"] = httpContext.TraceIdentifier;

                if (ex is IdempotencyPayloadConflictException)
                {
                    httpContext.Response.Headers["Idempotency-Outcome"] = "payload_conflict";
                    httpContext.Response.Headers["Idempotency-Replayed"] = "false";
                    activity?.SetTag("idempotency.outcome", "payload_conflict");
                    activity?.SetTag("idempotency.replayed", false);
                }

                httpContext.Response.StatusCode = status;
                await httpContext.Response.WriteAsJsonAsync(
                    response,
                    options: null,
                    contentType: "application/problem+json");
            }
        }
    }

    /// <summary>
    /// ExceptionHandlingMiddleware Helper class
    /// </summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        /// <summary>
        /// Add ExceptionHandlingMiddleware to middleware pipeline
        /// </summary>
        /// <param name="builder">applicationBuilder</param>
        /// <returns></returns>
        public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
