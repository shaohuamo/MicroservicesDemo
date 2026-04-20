namespace ProductsMicroservice.Core.MessageQueue.Messages
{
    public record ProductAddMessage(Guid ProductId, string? ProductName, double? UnitPrice, int? QuantityInStock);
}
