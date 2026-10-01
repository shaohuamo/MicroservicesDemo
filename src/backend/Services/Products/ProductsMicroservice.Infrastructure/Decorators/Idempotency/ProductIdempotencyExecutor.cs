using Microsoft.Extensions.Options;
using ProductsMicroservice.Core.Domain;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Options;
using ProductsMicroservice.Core.ServiceContracts;
using System.Security.Cryptography;
using System.Text.Json;

namespace ProductsMicroservice.Infrastructure.Decorators.Idempotency;

internal sealed class ProductIdempotencyExecutor(
    IIdempotencyRepository repository,
    IUnitOfWork unitOfWork,
    IProductOperationContextAccessor contextAccessor,
    IOptions<IdempotencyOptions> options,
    ProductIdempotencyResultCache cache)
{
    public async Task<(TResponse Response, bool IsReplay, IdempotencyResultSource Source)> ExecuteAsync<TResponse>(
        IdempotencyOperation operation, Guid key, string requestHash, int statusCode,
        Func<Task<TResponse>> execute)
    {
        string userId = contextAccessor.GetCurrent().UserId;
        var cached = await cache.TryReadAsync<TResponse>(userId, operation, key, statusCode);
        if (cached.Record is not null)
        {
            return (ReadResponse<TResponse>(cached.Record, requestHash), true, IdempotencyResultSource.Redis);
        }

        IdempotencyRecord? existing = await repository.GetAsync(userId, operation, key);
        if (existing is not null)
        {
            TResponse replay = ReadResponse<TResponse>(existing, requestHash);
            if (cached.Available) await cache.TryWriteAsync(existing);
            return (replay, true, IdempotencyResultSource.Database);
        }

        TResponse response;
        IdempotencyRecord record;
        try
        {
            response = await execute();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            record = new IdempotencyRecord
            {
                Id = Guid.CreateVersion7(), UserId = userId, Operation = operation,
                IdempotencyKey = key, RequestHash = requestHash, ResponseStatusCode = statusCode,
                ResponseJson = JsonSerializer.Serialize(response, JsonSerializerOptions.Web),
                CreatedAtUtc = now, ExpiresAtUtc = now.AddHours(options.Value.RetentionHours)
            };
            repository.Add(record);
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            unitOfWork.DiscardPendingChanges();
            if (exception is IdempotencyRecordConflictException or ProductAlreadyExistsException or
                ProductConcurrencyException or ProductNotFoundException)
            {
                // A duplicate can lose at a business precheck or at SaveChanges. Only a
                // committed matching result proves that this was the same operation.
                existing = await repository.GetAsync(userId, operation, key);
                if (existing is not null)
                {
                    TResponse replay = ReadResponse<TResponse>(existing, requestHash);
                    if (cached.Available) await cache.TryWriteAsync(existing);
                    return (replay, true, IdempotencyResultSource.Database);
                }
            }
            throw;
        }

        if (cached.Available) await cache.TryWriteAsync(record);
        return (response, false, IdempotencyResultSource.Executed);
    }

    private static TResponse ReadResponse<TResponse>(IdempotencyRecord record, string requestHash)
    {
        if (!CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(record.RequestHash), Convert.FromHexString(requestHash)))
        {
            throw new IdempotencyPayloadConflictException();
        }
        return ProductIdempotencyResultCache.DeserializeResponse<TResponse>(record);
    }
}
