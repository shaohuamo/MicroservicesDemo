using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Revocation;

public enum RevocationResult { Allowed, Denied, Unavailable }

public sealed class AccessTokenRevocationValidator(
    IRedisRevocationGuard redis,
    SessionProofVerifier proof,
    IServiceProvider services,
    ILogger<AccessTokenRevocationValidator> logger)
{
    public const string UnavailableItem = "gateway.session_fallback_unavailable";
    private static readonly Meter Meter = new("ApiGateway.Authentication");
    private static readonly Counter<long> Fallback = Meter.CreateCounter<long>("gateway_auth_fallback_total");
    private static readonly Counter<long> ProofFailure = Meter.CreateCounter<long>("gateway_auth_proof_failure_total");
    private static readonly Counter<long> DatabaseFailure = Meter.CreateCounter<long>("gateway_auth_database_failure_total");

    public async Task<RevocationResult> ValidateAsync(HttpContext context, string? jti, string accessToken)
    {
        if (string.IsNullOrWhiteSpace(jti) || string.IsNullOrWhiteSpace(accessToken))
            return RevocationResult.Denied;
        var (denied, fallback) = await redis.CheckAsync(jti);
        if (denied) return RevocationResult.Denied;
        if (!fallback) return RevocationResult.Allowed;
        Fallback.Add(1);
        if (!proof.TryVerify(context.Request.Headers, accessToken, out var id))
        {
            ProofFailure.Add(1);
            return RevocationResult.Denied;
        }

        var sessions = services.GetService<IRefreshSessionStore>();
        if (sessions is null) return RevocationResult.Denied;

        try
        {
            return await sessions.ExistsAsync(id, context.RequestAborted)
                ? RevocationResult.Allowed : RevocationResult.Denied;
        }
        catch (Exception error)
        {
            DatabaseFailure.Add(1);
            logger.LogError(error, "Gateway session fallback database query failed.");
            return RevocationResult.Unavailable;
        }
    }
}
