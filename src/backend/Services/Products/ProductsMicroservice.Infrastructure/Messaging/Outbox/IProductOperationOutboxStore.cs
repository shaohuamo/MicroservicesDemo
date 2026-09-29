namespace ProductsMicroservice.Infrastructure.Messaging.Outbox;

internal interface IProductOperationOutboxStore
{
    Task<IReadOnlyList<ProductOperationOutbox>> ClaimBatchAsync(
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> MarkPublishedAsync(
        ProductOperationOutbox outbox,
        string workerId,
        CancellationToken cancellationToken = default);

    Task ScheduleRetryAsync(
        ProductOperationOutbox outbox,
        string workerId,
        int attempt,
        TimeSpan retryDelay,
        string error,
        CancellationToken cancellationToken = default);
}
