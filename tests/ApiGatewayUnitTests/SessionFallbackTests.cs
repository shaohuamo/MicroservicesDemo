using System.Security.Cryptography;
using System.Text;
using ApiGateway.Revocation;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ApiGatewayUnitTests;

public sealed class SessionFallbackTests
{
    private const string Token = "signed.jwt.access-token";
    private static readonly Guid SessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");

    [Fact]
    public void ProofVerifier_AcceptsCurrentKey_ButRejectsUnknownKeyForgeryAndExpiredProof()
    {
        var proof = Verifier();
        var headers = SignedHeaders("v1", Key, Token);
        proof.TryVerify(headers, Token, out var id).Should().BeTrue();
        id.Should().Be(SessionId);
        proof.TryVerify(headers, "another.jwt.token", out _).Should().BeFalse();
        headers[SessionProofHeaders.Signature] = "forged";
        proof.TryVerify(headers, Token, out _).Should().BeFalse();

        headers = SignedHeaders("v0", Key, Token);
        proof.TryVerify(headers, Token, out _).Should().BeFalse();
        headers = SignedHeaders("v1", Key, Token, DateTimeOffset.UtcNow.AddSeconds(-31));
        proof.TryVerify(headers, Token, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, RevocationResult.Denied)]
    [InlineData(false, false, RevocationResult.Allowed)]
    [InlineData(false, true, RevocationResult.Allowed)]
    public async Task ValidateAsync_RedisResultControlsFallback(
        bool denied, bool fallback, RevocationResult expected)
    {
        var redis = new Mock<IRedisRevocationGuard>();
        redis.Setup(x => x.CheckAsync("jti-1")).ReturnsAsync((denied, fallback));
        var store = new Mock<IRefreshSessionStore>();
        store.Setup(x => x.ExistsAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = Validator(redis, store);
        var context = new DefaultHttpContext();
        if (fallback) CopyHeaders(context, SignedHeaders("v1", Key, Token));

        (await validator.ValidateAsync(context, "jti-1", Token)).Should().Be(expected);
        store.Verify(x => x.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            fallback && !denied ? Times.Once : Times.Never);
    }

    [Fact]
    public async Task ValidateAsync_DuringRedisOutage_RejectsDirectOrLoggedOutSession_AndReturnsUnavailableOnDatabaseFailure()
    {
        var redis = new Mock<IRedisRevocationGuard>();
        redis.Setup(x => x.CheckAsync("jti-1")).ReturnsAsync((false, true));
        var store = new Mock<IRefreshSessionStore>();
        var validator = Validator(redis, store);
        var context = new DefaultHttpContext();
        (await validator.ValidateAsync(context, "jti-1", Token)).Should().Be(RevocationResult.Denied);

        CopyHeaders(context, SignedHeaders("v1", Key, Token));
        store.Setup(x => x.ExistsAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        (await validator.ValidateAsync(context, "jti-1", Token)).Should().Be(RevocationResult.Denied);

        store.Setup(x => x.ExistsAsync(SessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));
        (await validator.ValidateAsync(context, "jti-1", Token)).Should().Be(RevocationResult.Unavailable);
    }

    [Fact]
    public void RecoveryState_ProtectsAfterFailureAndProcessRestart()
    {
        var state = new RedisRecoveryState();
        var now = DateTimeOffset.UtcNow;
        state.Observe("first", 5000, now).Should().BeFalse();
        state.Failed();
        state.Observe("first", 5001, now.AddSeconds(1)).Should().BeTrue();
        state.Observe("first", 5931, now.AddSeconds(931)).Should().BeFalse();
        state.Observe("second", 1, now.AddSeconds(932)).Should().BeTrue();
        state.Observe("second", 931, now.AddSeconds(1862)).Should().BeFalse();

        new RedisRecoveryState().Observe("new", 10, now).Should().BeTrue();
        new RedisRecoveryState().Observe("old", 2000, now).Should().BeFalse();
    }

    [Fact]
    public void ProofHeaders_AreRemovedBeforeDownstreamRouting()
    {
        var headers = SignedHeaders("v1", Key, Token);
        headers["Authorization"] = $"Bearer {Token}";
        SessionProofHeaders.Remove(headers);
        headers.ContainsKey(SessionProofHeaders.Id).Should().BeFalse();
        headers.ContainsKey(SessionProofHeaders.Signature).Should().BeFalse();
        headers["Authorization"].ToString().Should().Be($"Bearer {Token}");
    }

    private static AccessTokenRevocationValidator Validator(
        Mock<IRedisRevocationGuard> redis, Mock<IRefreshSessionStore> store) =>
        new(redis.Object, Verifier(), new ServiceCollection().AddSingleton(store.Object).BuildServiceProvider(),
            NullLogger<AccessTokenRevocationValidator>.Instance);

    private static SessionProofVerifier Verifier() => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:SessionFallback:Enabled"] = "true",
            ["Authentication:SessionFallback:ProofKeys:Current:Id"] = "v1",
            ["Authentication:SessionFallback:ProofKeys:Current:Secret"] = Convert.ToBase64String(Key),
        }).Build(), TimeProvider.System);

    private static HeaderDictionary SignedHeaders(string kid, byte[] key, string token,
        DateTimeOffset? time = null)
    {
        var timestamp = (time ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString();
        var tokenHash = Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var payload = $"{kid}\n{SessionId:D}\n{timestamp}\n{tokenHash}";
        return new HeaderDictionary
        {
            [SessionProofHeaders.Id] = SessionId.ToString("D"),
            [SessionProofHeaders.Timestamp] = timestamp,
            [SessionProofHeaders.KeyId] = kid,
            [SessionProofHeaders.Signature] = Base64Url(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload)))
        };
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static void CopyHeaders(DefaultHttpContext context, HeaderDictionary headers)
    {
        foreach (var header in headers) context.Request.Headers[header.Key] = header.Value;
    }
}
