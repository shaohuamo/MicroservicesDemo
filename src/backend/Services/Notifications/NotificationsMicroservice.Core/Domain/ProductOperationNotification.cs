namespace NotificationsMicroservice.Core.Domain;

public sealed record ProductOperationNotification(
    Guid NotificationId,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    string UserId,
    string UserEmail,
    string Culture,
    string Operation,
    string Status,
    Guid? ProductId,
    string? ProductName,
    int? ProductVersion,
    string? ErrorCode,
    string? TraceParent = null,
    string? TraceState = null);
