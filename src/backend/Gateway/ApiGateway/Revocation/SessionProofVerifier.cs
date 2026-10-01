using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace ApiGateway.Revocation;

public sealed class SessionProofVerifier(IConfiguration configuration, TimeProvider clock)
{
    public bool IsConfigured => configuration.GetValue<bool>("Authentication:SessionFallback:Enabled")
        && !string.IsNullOrWhiteSpace(configuration["Authentication:SessionFallback:ProofKeys:Current:Id"])
        && !string.IsNullOrWhiteSpace(configuration["Authentication:SessionFallback:ProofKeys:Current:Secret"]);

    public bool TryVerify(IHeaderDictionary headers, string accessToken, out Guid sessionId)
    {
        sessionId = default;
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        if (!IsConfigured
            || !TrySingle(headers[SessionProofHeaders.Id], out var id)
            || !Guid.TryParseExact(id, "D", out sessionId)
            || !TrySingle(headers[SessionProofHeaders.Timestamp], out var timestamp)
            || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || !TrySingle(headers[SessionProofHeaders.KeyId], out var keyId)
            || !TrySingle(headers[SessionProofHeaders.Signature], out var signature)
            || seconds < now - 30
            || seconds > now + 30)
        {
            return false;
        }

        var configuredId = configuration["Authentication:SessionFallback:ProofKeys:Current:Id"];
        var secret = configuration["Authentication:SessionFallback:ProofKeys:Current:Secret"];
        if (keyId != configuredId || string.IsNullOrWhiteSpace(secret)) return false;

        byte[] key;
        byte[] provided;
        try
        {
            key = Convert.FromBase64String(secret);
            provided = Base64UrlDecode(signature);
        }
        catch (FormatException) { return false; }
        if (key.Length < 32 || provided.Length != 32) return false;

        var hash = Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));
        var payload = $"{keyId}\n{sessionId:D}\n{timestamp}\n{hash}";
        var expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload));
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    private static bool TrySingle(StringValues values, out string value)
    {
        value = values.ToString();
        return values.Count == 1 && !string.IsNullOrWhiteSpace(value);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
    }

    private static string Base64UrlEncode(byte[] value) => Convert.ToBase64String(value)
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
