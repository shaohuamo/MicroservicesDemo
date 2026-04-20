using System.Diagnostics;

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
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);

                var activity = Activity.Current;
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);

                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

                var response = new
                {
                    Message = "Internal Server Error",
                    Type = ex.GetType().Name,
                    Detail = ex.InnerException?.Message ?? ex.Message
                };

                await httpContext.Response.WriteAsJsonAsync(response);
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
