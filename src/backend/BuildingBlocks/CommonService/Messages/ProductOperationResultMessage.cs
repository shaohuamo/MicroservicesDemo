namespace CommonService.Messages;

/// <summary>
/// The product command that produced a user-facing notification.
/// </summary>
public enum ProductOperation
{
    Add,
    Update,
    Delete
}

/// <summary>
/// The business outcome of a product command.
/// </summary>
public enum ProductOperationStatus
{
    Success,
    Failure
}

/// <summary>
/// Durable integration event emitted after a product command and its outbox row
/// have committed in the same database transaction.
/// </summary>
public sealed record ProductOperationResultMessage(
    Guid NotificationId,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    string UserId,
    string UserEmail,
    string Culture,
    ProductOperation Operation,
    ProductOperationStatus Status,
    Guid ProductId,
    string? ProductName,
    int? ProductVersion,
    string? ErrorCode);
