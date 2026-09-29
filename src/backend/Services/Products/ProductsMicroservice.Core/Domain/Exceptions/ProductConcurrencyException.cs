namespace ProductsMicroservice.Core.Domain.Exceptions;

public sealed class ProductConcurrencyException : Exception
{
    public ProductConcurrencyException(Guid? productId = null, Exception? innerException = null)
        : base("The product was modified or deleted by another request. Refresh it and try again.", innerException)
    {
        ProductId = productId;
    }

    public Guid? ProductId { get; }
}
