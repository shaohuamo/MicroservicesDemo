using ProductsMicroservice.Core.Domain.Services;
using ProductsMicroservice.Core.DTO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProductsMicroservice.Infrastructure.Decorators.Idempotency;

internal static class ProductRequestFingerprint
{
    public static string ForAdd(ProductAddRequest request) => Hash(new
    {
        ProductNameNormalizer.Normalize(request.DisplayName).DisplayName,
        request.UnitPrice,
        request.QuantityInStock
    });

    public static string ForUpdate(ProductUpdateRequest request) => Hash(new
    {
        request.ProductId,
        request.Version,
        ProductNameNormalizer.Normalize(request.DisplayName).DisplayName,
        request.UnitPrice,
        request.QuantityInStock
    });

    public static string ForDelete(Guid productId, int version) => Hash(new { ProductId = productId, Version = version });

    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonSerializerOptions.Web))));
}
