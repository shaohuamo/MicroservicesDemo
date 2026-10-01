using System.Text.Json;

namespace NotificationsMicroservice.Core.DTO;

/// <summary>A stable ascending replay boundary, including the scan's sequence watermark.</summary>
public sealed record NotificationReplayCursor(long Watermark, DateTimeOffset AfterOccurredAtUtc, long AfterSequence)
{
    public string Encode() => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(this));

    public static bool TryDecode(string token, out NotificationReplayCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 1024) return false;
        try
        {
            var decoded = JsonSerializer.Deserialize<NotificationReplayCursor>(Convert.FromBase64String(token));
            // Legacy Before... cursors deserialize with empty After... boundaries and are rejected.
            if (decoded is null || decoded.Watermark <= 0 || decoded.AfterSequence <= 0
                || decoded.AfterSequence > decoded.Watermark || decoded.AfterOccurredAtUtc == default) return false;
            cursor = decoded with { AfterOccurredAtUtc = decoded.AfterOccurredAtUtc.ToUniversalTime() };
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }
    }
}
