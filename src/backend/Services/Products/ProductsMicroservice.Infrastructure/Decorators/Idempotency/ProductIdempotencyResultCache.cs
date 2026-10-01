using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.Domain;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.DTO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProductsMicroservice.Infrastructure.Decorators.Idempotency;

internal sealed class ProductIdempotencyResultCache(
    IDistributedCache cache,
    ILogger<ProductIdempotencyResultCache> logger)
{
    internal static string CreateKey(string userId, IdempotencyOperation operation, Guid key) =>
        $"products:idempotency:v1:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId)))}:{operation}:{key:D}";

    public async Task<(bool Available, IdempotencyRecord? Record)> TryReadAsync<TResponse>(
        string userId, IdempotencyOperation operation, Guid key, int statusCode)
    {
        string? json;
        try
        {
            json = await cache.GetStringAsync(CreateKey(userId, operation, key));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Idempotency cache read failed; falling back to PostgreSQL");
            return (false, null);
        }

        if (json is null)
        {
            return (true, null);
        }

        try
        {
            IdempotencyRecord? record = JsonSerializer.Deserialize<IdempotencyRecord>(json, JsonSerializerOptions.Web);
            if (record is null || record.UserId != userId || record.Operation != operation ||
                record.IdempotencyKey != key || record.ResponseStatusCode != statusCode ||
                record.ExpiresAtUtc <= DateTimeOffset.UtcNow || record.RequestHash is not { Length: 64 } ||
                !record.RequestHash.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(record.ResponseJson))
            {
                return (true, null);
            }

            DeserializeResponse<TResponse>(record);
            return (true, record);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            logger.LogWarning(exception, "Invalid idempotency cache value; falling back to PostgreSQL");
            return (true, null);
        }
    }

    public async Task TryWriteAsync(IdempotencyRecord record)
    {
        if (record.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return;
        }

        try
        {
            await cache.SetStringAsync(CreateKey(record.UserId, record.Operation, record.IdempotencyKey),
                JsonSerializer.Serialize(record, JsonSerializerOptions.Web),
                new DistributedCacheEntryOptions { AbsoluteExpiration = record.ExpiresAtUtc });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Idempotency cache write failed; the committed PostgreSQL result remains available");
        }
    }

    internal static TResponse DeserializeResponse<TResponse>(IdempotencyRecord record)
    {
        TResponse? response = JsonSerializer.Deserialize<TResponse>(record.ResponseJson, JsonSerializerOptions.Web);
        if (response is null || response is false ||
            response is ProductResponse product && (product.ProductId == Guid.Empty || product.Version < 1))
        {
            throw new InvalidDataException("Stored idempotency response is invalid.");
        }
        return response;
    }
}
