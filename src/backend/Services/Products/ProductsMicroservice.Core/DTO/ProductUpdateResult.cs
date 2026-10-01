namespace ProductsMicroservice.Core.DTO;

public sealed record ProductUpdateResult(ProductResponse Product, bool IsReplay)
{
    public IdempotencyResultSource Source { get; init; } = IdempotencyResultSource.Executed;
}
