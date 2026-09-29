namespace ProductsMicroservice.Core.DTO;

public record ProductResponse(
    Guid ProductId,
    string? DisplayName,
    decimal UnitPrice,
    int QuantityInStock,
    int Version = default)
{
    // used for automapper
    public ProductResponse() : this(default, default, default, default, default)
    {
    }
}
