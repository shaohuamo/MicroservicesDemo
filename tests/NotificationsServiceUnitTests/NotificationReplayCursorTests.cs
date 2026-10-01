using System.Text;
using FluentAssertions;
using NotificationsMicroservice.Core.DTO;

namespace NotificationsMicroservice.Tests;

public sealed class NotificationReplayCursorTests
{
    #region Round trip and validation

    [Fact]
    public void Encode_RoundTrip_PreservesTimestampPrecisionAndWatermark()
    {
        var timestamp = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(8)).AddTicks(1234560);
        var expected = new NotificationReplayCursor(200, timestamp, 53);
        NotificationReplayCursor.TryDecode(expected.Encode(), out var actual).Should().BeTrue();
        actual.Should().BeEquivalentTo(expected, options => options.Excluding(value => value.BeforeOccurredAtUtc.Offset));
        actual!.BeforeOccurredAtUtc.Should().Be(timestamp);
        actual.BeforeOccurredAtUtc.Offset.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-base64")]
    [InlineData("bnVsbA==")]
    [InlineData("e30=")]
    public void TryDecode_InvalidToken_ReturnsFalse(string token)
    {
        NotificationReplayCursor.TryDecode(token, out var cursor).Should().BeFalse();
        cursor.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    [InlineData(10, 11)]
    public void TryDecode_InvalidSequenceBoundary_ReturnsFalse(long watermark, long beforeSequence)
    {
        var token = new NotificationReplayCursor(watermark, DateTimeOffset.UtcNow, beforeSequence).Encode();
        NotificationReplayCursor.TryDecode(token, out _).Should().BeFalse();
    }

    [Fact]
    public void TryDecode_MalformedTimestampAndOversizedToken_ReturnsFalse()
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"Watermark\":10,\"BeforeSequence\":5,\"BeforeOccurredAtUtc\":\"invalid\"}"));
        NotificationReplayCursor.TryDecode(token, out _).Should().BeFalse();
        NotificationReplayCursor.TryDecode(new string('a', 1025), out _).Should().BeFalse();
    }

    #endregion
}
