using System.Text.Json;

namespace NotificationsMicroservice.Core.DTO;

/// <summary>A stable descending replay boundary, including the scan's sequence watermark.</summary>
public sealed record NotificationReplayCursor(long Watermark, DateTimeOffset BeforeOccurredAtUtc, long BeforeSequence)
{
    public string Encode() => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(this));

    public static bool TryDecode(string token, out NotificationReplayCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 1024) return false;
        try
        {
            var decoded = JsonSerializer.Deserialize<NotificationReplayCursor>(Convert.FromBase64String(token));
            if (decoded is null || decoded.Watermark <= 0 || decoded.BeforeSequence <= 0
                || decoded.BeforeSequence > decoded.Watermark || decoded.BeforeOccurredAtUtc == default) return false;
            cursor = decoded with { BeforeOccurredAtUtc = decoded.BeforeOccurredAtUtc.ToUniversalTime() };
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }
    }
}
