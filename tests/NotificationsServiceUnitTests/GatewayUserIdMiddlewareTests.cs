using CommonService.Middlewares;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using NotificationsMicroservice.API.Middleware;
using OpenTelemetry;

namespace NotificationsMicroservice.Tests;

public class GatewayUserIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ValidGatewayUserId_InvokesNext()
    {
        var context = NewContext("/api/notifications", "current-user");
        var called = false;
        var middleware = new GatewayUserIdMiddleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MaximumLengthUserId_InvokesNext()
    {
        var context = NewContext("/api/notifications", new string('u', 256));
        var called = false;
        var middleware = new GatewayUserIdMiddleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("anonymous")]
    [InlineData(" padded ")]
    public async Task InvokeAsync_InvalidGatewayUserId_ReturnsUnauthorized(string? userId)
    {
        var context = NewContext("/api/notifications", userId);
        var called = false;
        var middleware = new GatewayUserIdMiddleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        called.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_MultipleUserIds_ReturnsUnauthorized()
    {
        var context = NewContext("/api/notifications/replay", "user-one");
        context.Request.Headers["X-User-Id"] = new StringValues(["user-one", "user-two"]);

        await new GatewayUserIdMiddleware(_ => Task.CompletedTask).InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_TooLongUserId_ReturnsUnauthorized()
    {
        var context = NewContext("/api/notifications", new string('u', 257));

        await new GatewayUserIdMiddleware(_ => Task.CompletedTask).InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_MismatchedTraceUserId_ReturnsUnauthorized()
    {
        var context = NewContext("/api/notifications", "gateway-user");
        context.Items[TraceContextMiddleware.UserIdItemKey] = "baggage-user";

        await new GatewayUserIdMiddleware(_ => Task.CompletedTask).InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_MissingTraceUserId_ReturnsUnauthorized()
    {
        var context = NewContext("/api/notifications", "gateway-user");
        context.Items.Remove(TraceContextMiddleware.UserIdItemKey);

        await new GatewayUserIdMiddleware(_ => Task.CompletedTask).InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_BaggageOnlyUserId_ReturnsUnauthorized()
    {
        var previousBaggage = Baggage.Current;
        try
        {
            Baggage.SetBaggage("user_id", "baggage-user");
            var context = NewContext("/api/notifications", null);
            var gatewayMiddleware = new GatewayUserIdMiddleware(_ => Task.CompletedTask);
            var traceMiddleware = new TraceContextMiddleware(gatewayMiddleware.InvokeAsync);

            await traceMiddleware.Invoke(context, NullLogger<TraceContextMiddleware>.Instance);

            context.Items[TraceContextMiddleware.UserIdItemKey].Should().Be("baggage-user");
            context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
        finally
        {
            Baggage.Current = previousBaggage;
        }
    }

    [Fact]
    public async Task InvokeAsync_HealthCheckWithoutUserId_InvokesNext()
    {
        var context = NewContext("/health/ready", null);
        var called = false;
        var middleware = new GatewayUserIdMiddleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }

    private static DefaultHttpContext NewContext(string path, string? userId)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (userId is not null)
        {
            context.Request.Headers["X-User-Id"] = userId;
            context.Items[TraceContextMiddleware.UserIdItemKey] = userId;
        }

        return context;
    }
}
