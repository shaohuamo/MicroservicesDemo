namespace ProductsMicroservice.Infrastructure.Options;

public sealed class ProductOperationMessagingOptions
{
    public const string SectionName = "ProductOperationMessaging";

    public string ExchangeName { get; set; } = "products.operations";
    public string RoutingKey { get; set; } = "products.operation.completed";
}
