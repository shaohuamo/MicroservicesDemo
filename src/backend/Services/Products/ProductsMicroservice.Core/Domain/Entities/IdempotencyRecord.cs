namespace ProductsMicroservice.Core.Domain.Entities;

public sealed class IdempotencyRecord
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public IdempotencyOperation Operation { get; init; }
    public Guid IdempotencyKey { get; init; }
    public string RequestHash { get; init; } = string.Empty;
    public int ResponseStatusCode { get; init; }
    public string ResponseJson { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset ExpiresAtUtc { get; init; }
}
