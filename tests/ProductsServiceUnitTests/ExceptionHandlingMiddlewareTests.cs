using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using CommonService.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroService.API.Middleware;

namespace ProductsMicroservice.Tests;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock = new();

    [Fact]
    public async Task Invoke_ShouldCallNextMiddleware_WhenNoExceptionOccurs()
    {
        var nextCalled = false;
        var middleware = new ExceptionHandlingMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            _loggerMock.Object);

        await middleware.Invoke(new DefaultHttpContext());

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Invoke_ShouldReturnStructuredError_WhenNextMiddlewareThrows()
    {
        var exception = new InvalidOperationException("outer", new Exception("inner"));
        var middleware = new ExceptionHandlingMiddleware(_ => throw exception, _loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        using var activity = new Activity("middleware-test").Start();

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().StartWith("application/problem+json");
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        json.RootElement.GetProperty("title").GetString().Should().Be("Internal Server Error");
        json.RootElement.GetProperty("status").GetInt32().Should().Be(500);
        json.RootElement.GetProperty("detail").GetString().Should().Be("An unexpected error occurred.");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("internal_error");
        activity.Status.Should().Be(ActivityStatusCode.Error);
    }

    [Theory]
    [InlineData(true, LogLevel.Warning, 404)]
    [InlineData(false, LogLevel.Error, 500)]
    public async Task Invoke_WhenRequestHasResolvedUserId_LogsUserIdWithException(
        bool notFound,
        LogLevel expectedLogLevel,
        int expectedStatusCode)
    {
        Exception exception = notFound
            ? new ProductNotFoundException(Guid.NewGuid())
            : new InvalidOperationException("unexpected failure");
        var logger = new CapturingLogger<ExceptionHandlingMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Id"] = "user-123";
        context.Response.Body = new MemoryStream();
        var traceMiddleware = new TraceContextMiddleware(_ => Task.FromException(exception));
        var middleware = new ExceptionHandlingMiddleware(
            request => traceMiddleware.Invoke(request, NullLogger<TraceContextMiddleware>.Instance),
            logger);
        using var activity = new Activity("request").Start();

        await middleware.Invoke(context);

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.LogLevel.Should().Be(expectedLogLevel);
        entry.Scopes.Should().ContainKey("UserId").WhoseValue.Should().Be("user-123");
        entry.TraceId.Should().Be(activity.TraceId.ToString());
        context.Response.StatusCode.Should().Be(expectedStatusCode);
    }

    [Fact]
    public async Task Invoke_WhenResolvedUserIdIsUnavailable_LogsAnonymousInsteadOfRequestHeader()
    {
        var logger = new CapturingLogger<ExceptionHandlingMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Id"] = "untrusted-user";
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("unexpected failure"),
            logger);

        await middleware.Invoke(context);

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Scopes.Should().ContainKey("UserId").WhoseValue.Should().Be("anonymous");
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact]
    public async Task Invoke_ShouldReturnConflict_WhenProductVersionIsStale()
    {
        Guid productId = Guid.NewGuid();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ProductConcurrencyException(productId),
            _loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        json.RootElement.GetProperty("errorCode").GetString()
            .Should().Be("product.concurrency_conflict");
    }

    [Fact]
    public async Task Invoke_ShouldReturnNotFound_WhenProductDoesNotExist()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ProductNotFoundException(Guid.NewGuid()),
            _loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("product.not_found");
    }

    [Theory]
    [InlineData(true, 400, "idempotency.key_invalid")]
    [InlineData(false, 409, "idempotency.payload_conflict")]
    public async Task Invoke_ShouldMapIdempotencyErrors(
        bool invalidKey, int expectedStatus, string expectedCode)
    {
        Exception exception = invalidKey
            ? new IdempotencyKeyInvalidException()
            : new IdempotencyPayloadConflictException();
        var middleware = new ExceptionHandlingMiddleware(_ => throw exception, _loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(expectedStatus);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be(expectedCode);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly List<object> _scopes = [];

        public List<CapturedLog> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            _scopes.Add(state);
            return new Scope(() => _scopes.Remove(state));
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scopeValues = _scopes
                .OfType<IEnumerable<KeyValuePair<string, object>>>()
                .SelectMany(scope => scope)
                .ToDictionary(pair => pair.Key, pair => (object?)pair.Value);
            Entries.Add(new CapturedLog(logLevel, scopeValues, Activity.Current?.TraceId.ToString()));
        }

        public sealed record CapturedLog(
            LogLevel LogLevel,
            IReadOnlyDictionary<string, object?> Scopes,
            string? TraceId);

        private sealed class Scope(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }
}
