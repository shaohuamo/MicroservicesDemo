namespace ProductsMicroservice.Core.DTO;

public sealed record ProductAddResult(ProductResponse Product, bool IsReplay)
{
    public IdempotencyResultSource Source { get; init; } = IdempotencyResultSource.Executed;
}
