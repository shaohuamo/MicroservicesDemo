namespace ProductsMicroservice.Core.CacheKeys
{
    public static class ProductCacheKeys
    {
        // v2 entries include the optimistic-concurrency Version field.
        private const string BasePrefix = "product:v2";

        /// <summary>
        /// Product cache key (e.g., product:v2:guid).
        /// </summary>
        public static string GetDetailsKey(Guid productId) => $"{BasePrefix}:{productId}";

        public static string AllProductsKey => "all-products:v2";
    }
}
