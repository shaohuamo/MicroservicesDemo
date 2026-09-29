using CommonService.Messages;

namespace ProductsMicroservice.Core.Messaging.OutboxWriterContracts;

/// <summary>
/// Adds an integration event to the product operation outbox in the current
/// unit-of-work transaction.
/// </summary>
public interface IProductOperationOutboxWriter
{
    Task WriteAsync(
        ProductOperationResultMessage message,
        CancellationToken cancellationToken = default);
}
