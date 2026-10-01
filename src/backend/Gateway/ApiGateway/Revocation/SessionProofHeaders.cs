using Microsoft.AspNetCore.Http;

namespace ApiGateway.Revocation;

public static class SessionProofHeaders
{
    public const string Id = "X-Admin-Session-Id";
    public const string Timestamp = "X-Admin-Proof-Iat";
    public const string KeyId = "X-Admin-Proof-Kid";
    public const string Signature = "X-Admin-Proof-Sig";

    public static void Remove(IHeaderDictionary headers)
    {
        headers.Remove(Id);
        headers.Remove(Timestamp);
        headers.Remove(KeyId);
        headers.Remove(Signature);
    }
}
