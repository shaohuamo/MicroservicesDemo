namespace ProductsMicroservice.Core.DTO;

public enum IdempotencyResultSource
{
    Executed,
    Redis,
    Database
}
