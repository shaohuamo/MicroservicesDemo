using System.Security.Claims;
using ApiGateway.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace ApiGatewayUnitTests;

public class IdentityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_ReplacesCallerSuppliedIdentityHeaders()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Id"] = "spoofed-user";
        context.Request.Headers["X-User-Email"] = "spoofed@example.com";
        context.Request.Headers["X-User-Role"] = "admin";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "validated-user"), new Claim("email", "real@example.com")], "Bearer"));
        var called = false;
        var middleware = new IdentityHeadersMiddleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
        context.Request.Headers["X-User-Id"].ToString().Should().Be("validated-user");
        context.Request.Headers["X-User-Email"].ToString().Should().Be("real@example.com");
        context.Request.Headers.ContainsKey("X-User-Role").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_AnonymousUser_RemovesCallerSuppliedIdentityHeaders()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-user-id"] = "spoofed-user";
        var middleware = new IdentityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Request.Headers.ContainsKey("X-User-Id").Should().BeFalse();
    }
}
