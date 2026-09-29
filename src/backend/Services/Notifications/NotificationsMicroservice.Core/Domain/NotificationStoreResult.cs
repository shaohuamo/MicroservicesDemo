namespace NotificationsMicroservice.Core.Domain;

public enum NotificationStoreResult
{
    Inserted,
    Duplicate,
    PayloadConflict
}
