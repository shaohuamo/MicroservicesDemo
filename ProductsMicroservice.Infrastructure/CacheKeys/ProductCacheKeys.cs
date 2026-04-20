namespace ProductsMicroservice.Core.CacheKeys
{
    public static class ProductCacheKeys
    {
        private const string BasePrefix = "product";

        /// <summary>
        /// product cahce key (e.g., product:guid)
        /// </summary>
        public static string GetDetailsKey(Guid productId) => $"{BasePrefix}:{productId}";

        public static string AllProductsKey => "all-products";
    }
}
