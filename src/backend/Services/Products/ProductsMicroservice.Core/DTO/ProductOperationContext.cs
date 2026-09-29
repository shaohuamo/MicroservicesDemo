namespace ProductsMicroservice.Core.DTO;

/// <summary>
/// Trusted identity and request metadata supplied by the gateway.
/// </summary>
public sealed record ProductOperationContext(
    string UserId,
    string UserEmail,
    string Culture,
    string CorrelationId);
