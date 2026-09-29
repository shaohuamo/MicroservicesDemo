using System.ComponentModel.DataAnnotations;

namespace NotificationsMicroservice.Core.DTO;

public sealed record ReadAllNotificationsRequest(
    [param: Range(1, long.MaxValue)] long UpToSequence);
