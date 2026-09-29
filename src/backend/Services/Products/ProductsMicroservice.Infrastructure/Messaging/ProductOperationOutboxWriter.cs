using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommonService.Messages;
using ProductsMicroservice.Core.Messaging.OutboxWriterContracts;
using ProductsMicroservice.Infrastructure.DbContext;
using ProductsMicroservice.Infrastructure.Messaging.Outbox;

namespace ProductsMicroservice.Infrastructure.Messaging;

internal sealed class ProductOperationOutboxWriter : IProductOperationOutboxWriter
{
    internal static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly ApplicationDbContext _dbContext;

    public ProductOperationOutboxWriter(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task WriteAsync(
        ProductOperationResultMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        string payload = JsonSerializer.Serialize(message, JsonOptions);
        string payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        _dbContext.ProductOperationOutbox.Add(new ProductOperationOutbox
        {
            NotificationId = message.NotificationId,
            OccurredAtUtc = message.OccurredAtUtc.UtcDateTime,
            Payload = payload,
            PayloadHash = payloadHash,
            TraceParent = Activity.Current?.Id,
            TraceState = Activity.Current?.TraceStateString,
            CreatedAtUtc = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
