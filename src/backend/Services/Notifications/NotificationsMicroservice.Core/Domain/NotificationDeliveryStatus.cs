namespace NotificationsMicroservice.Core.Domain;

public enum NotificationDeliveryStatus
{
    Pending,
    AwaitingSseAck,
    RetryScheduled,
    SendingEmail,
    DeliveredInApp,
    DeliveredEmail,
    Failed
}
