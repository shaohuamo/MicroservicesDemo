using CommonService.Messages;
using ProductsMicroservice.Core.DTO;

namespace ProductsMicroservice.Core.Messaging;

internal static class ProductOperationResultMessageFactory
{
    public static ProductOperationResultMessage Create(
        Guid notificationId,
        ProductOperationContext context,
        ProductOperation operation,
        ProductOperationStatus status,
        Guid productId,
        string? productName,
        int? productVersion,
        string? errorCode = null) =>
        new(notificationId, DateTimeOffset.UtcNow, context.CorrelationId, context.UserId,
            context.UserEmail, context.Culture, operation, status, productId, productName,
            productVersion, errorCode);
}
