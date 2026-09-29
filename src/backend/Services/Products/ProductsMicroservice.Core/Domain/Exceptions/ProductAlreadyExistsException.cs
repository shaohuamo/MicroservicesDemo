namespace ProductsMicroservice.Core.Domain.Exceptions;

public sealed class ProductAlreadyExistsException : Exception
{
    public ProductAlreadyExistsException(string? productName = null, Exception? innerException = null)
        : base(productName is null
            ? "A product with the same name already exists."
            : $"A product named '{productName}' already exists.", innerException)
    {
        ProductName = productName;
    }

    public string? ProductName { get; }
}
