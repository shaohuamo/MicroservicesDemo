using System.Diagnostics;

namespace NotificationsMicroservice.Infrastructure.Diagnostics;

public static class NotificationsTelemetry
{
    public const string ActivitySourceName = "Notifications";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
