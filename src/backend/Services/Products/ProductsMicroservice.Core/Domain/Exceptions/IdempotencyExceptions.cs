namespace ProductsMicroservice.Core.Domain.Exceptions;

public sealed class IdempotencyKeyInvalidException : Exception
{
    public IdempotencyKeyInvalidException()
        : base("Idempotency-Key must be a UUID v4 in canonical D format.") { }
}

public sealed class IdempotencyPayloadConflictException : Exception
{
    public IdempotencyPayloadConflictException()
        : base("The Idempotency-Key was already used with a different request payload.") { }
}

public sealed class IdempotencyRecordConflictException : Exception
{
    public IdempotencyRecordConflictException(Exception innerException)
        : base("The idempotency record was created by a concurrent request.", innerException) { }
}
