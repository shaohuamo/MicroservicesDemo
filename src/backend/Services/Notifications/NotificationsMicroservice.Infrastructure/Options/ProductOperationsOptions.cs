namespace NotificationsMicroservice.Infrastructure.Options;

public sealed class ProductOperationsOptions
{
    public const string SectionName = "ProductOperations";

    public string Exchange { get; set; } = "products.operations";
    public string RoutingKey { get; set; } = "products.operation.completed";
    public string Queue { get; set; } = "notifications.products.operations";
    public string DeadLetterExchange { get; set; } = "products.operations.dlx";
    public string DeadLetterQueue { get; set; } = "notifications.products.operations.dead-letter";
    public ushort PrefetchCount { get; set; } = 20;
}
