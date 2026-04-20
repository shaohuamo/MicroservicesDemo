namespace ProductsMicroservice.Core.DTO;

public record ProductResponse(Guid ProductId, string? ProductName, double? UnitPrice, int? QuantityInStock)
{
    // used for automapper
    public ProductResponse() : this(default, default, default, default)
    {
    }
}
