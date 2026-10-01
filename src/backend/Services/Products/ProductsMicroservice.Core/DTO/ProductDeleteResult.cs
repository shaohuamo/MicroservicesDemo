namespace ProductsMicroservice.Core.DTO;

public sealed record ProductDeleteResult(bool Deleted, bool IsReplay)
{
    public IdempotencyResultSource Source { get; init; } = IdempotencyResultSource.Executed;
}
