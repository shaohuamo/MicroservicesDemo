using Medallion.Threading;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.Diagnostics;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Core.Services;
using ProductsMicroservice.Infrastructure.Options;
using System.Diagnostics;
using System.Text.Json;

namespace ProductsMicroservice.Infrastructure.Decorators.Caching
{
    public class ProductsGetterCachingDecorator : IProductsGetterService
    {
        private readonly IProductsGetterService _innerService;
        private readonly IDistributedCache _distributedCache;
        private readonly IDistributedLockProvider _lockProvider;
        private readonly CacheOptions _cacheOptions;
        private readonly ILogger<ProductsGetterCachingDecorator> _logger;
        private static readonly SemaphoreSlim _localLock = new(1, 1);
        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IServiceScopeFactory _scopeFactory;

        public ProductsGetterCachingDecorator(
            IProductsGetterService inner, IDistributedCache cache,
            IDistributedLockProvider lockProvider, IOptions<CacheOptions> options,
            ILogger<ProductsGetterCachingDecorator> logger, IServiceScopeFactory scopeFactory)
        {
            _innerService = inner;
            _distributedCache = cache;
            _lockProvider = lockProvider;
            _cacheOptions = options.Value;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        #region GetProductByProductIdAsync
        public async Task<ProductResponse?> GetProductByProductIdAsync(Guid productId)
        {
            var activity = Activity.Current;
            string cacheKey = ProductCacheKeys.GetDetailsKey(productId);

            //010-000:get product by productId from cache
            string? cachedData = await _distributedCache.GetStringAsync(cacheKey);

            // 020-000:cache hit
            if (cachedData != null)
            {
                activity?.SetTag("cache.status", "hit");
                // cache stampede prevention for non-existent product:
                // if cachedData is a special placeholder value indicating null,
                // treat it as a cache hit but return null without hitting the database.
                if (cachedData == _cacheOptions.NullValuePlaceholder)
                {
                    activity?.SetTag("cache.hit_type", "negative");
                    activity?.AddEvent(new("Negative cache hit: Product does not exist."));
                    _logger.LogInformation("Negative Cache Hit for ProductId: {ProductId}", productId);
                    return null;
                }

                activity?.SetTag("cache.hit_type", "data");
                _logger.LogInformation("Cache Hit for ProductId: {ProductId}", productId);

                return JsonSerializer.Deserialize<ProductResponse>(cachedData, _jsonOptions);
            }

            // 030-000:cache miss
            activity?.SetTag("cache.status", "miss");
            _logger.LogInformation("Cache Miss. Fetching from database.");
            activity?.AddEvent(new("DB Fetch Started"));

            //call inner servcie
            var result = await _innerService.GetProductByProductIdAsync(productId);

            // 030-010:Cache the result to prevent cache stampede for subsequent requests with the same non-existent productId
            if (result == null)
            {
                activity?.SetTag("cache.fill_type", "negative_fill");
                activity?.AddEvent(new("Product not found in DB. Setting negative cache."));
                _logger.LogWarning("Product not found in DB. Setting negative cache.");

                var negativeOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheOptions.NegativeCacheExpirationMinutes)
                };

                await _distributedCache.SetStringAsync(cacheKey, _cacheOptions.NullValuePlaceholder, negativeOptions);
                return null;
            }
            // 030-020:Store normal data in cache with longer expiration
            // Invokes ProductToProductResponseMappingProfile
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheOptions.DefaultExpirationMinutes)
            };

            activity?.SetTag("cache.fill_type", "data_fill");
            activity?.AddEvent(new("Updating Redis with fresh data."));

            await _distributedCache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), cacheOptions);

            _logger.LogInformation("Product retrieved and cached successfully.");
            return result;
        }
        #endregion

        #region GetProductsAsync
        /// <summary>
        /// Logical Expire + localLock(SemaphoreSlim) + distributedLock (RedLock) to prevent cache stampede
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<IEnumerable<ProductResponse?>> GetProductsAsync()
        {
            var activity = Activity.Current;

            // 010-000:get data from cache
            string? productsFromCache = await _distributedCache.GetStringAsync(ProductCacheKeys.AllProductsKey);

            // 020-000:cache miss
            if (productsFromCache == null)
            {
                return await HandleHardCacheMiss(activity);
            }

            // 030-000:cache hit(but check logical expire time)
            return await HandleCacheHit(activity, productsFromCache);
        }

        /// <summary>
        /// 030-000:cache hit(but check logical expire time)
        /// </summary>
        /// <param name="activity"></param>
        /// <param name="cachedProducts"></param>
        /// <returns></returns>
        private async Task<IEnumerable<ProductResponse?>> HandleCacheHit(Activity? activity, string cachedProducts)
        {
            // 030-010:deserialize cached data and logical expire time
            var productsFromCache = JsonSerializer.Deserialize<RedisDataWrapper<List<ProductResponse>>>(cachedProducts, _jsonOptions);

            if (productsFromCache?.Data == null)
            {
                // In case of deserialization failure or data corruption(data was corrupted in redis)
                // treat it as a cache miss and fetch fresh data.
                return await HandleHardCacheMiss(activity);
            }

            // 030-020: expire time not passed(return cached data immediately)
            if (productsFromCache.LogicExpireTime > DateTime.Now)
            {
                activity?.SetTag("cache.status", "hit");
                activity?.SetTag("products.count", productsFromCache.Data.Count);
                _logger.LogInformation("Products retrieved from cache. Count: {ProductCount}", productsFromCache.Data.Count);
                return productsFromCache.Data;
            }

            // 030-030: logical expire time passed(trigger cache refresh in background and return stale data immediately)
            var parentContext = Activity.Current?.Context ?? default;

            // 030-031:acquire local lock
            // (ensure only one thread in this process attempts to acquire distributed lock and refresh cache.
            // prevent other threads in the same process from acquiring the distributed lock.)
            if (await _localLock.WaitAsync(0))
            {
                // trigger cache refresh in background without blocking current request to return stale data.
                _ = Task.Run(() => BackgroundRefresh(parentContext));
            }

            // 030-033: all threads(not acquire local lock or distributed lock and acquired distributed lock)
            // return stale data immediately without waiting
            activity?.SetTag("cache.status", "stale_hit");
            _logger.LogInformation("Returning stale data to avoid blocking.");
            return productsFromCache.Data;
        }

        /// <summary>
        /// 020-000:Cold Start: First request comes in, cache is empty, 
        /// and all requests must wait for the first DB fetch to populate the cache.
        /// </summary>
        /// <param name="activity"></param>
        /// <returns></returns>
        private async Task<IEnumerable<ProductResponse?>> HandleHardCacheMiss(Activity? activity)
        {
            activity?.SetTag("cache.status", "miss");
            _logger.LogWarning("Hard cache miss. All requests must wait for the first DB fetch.");

            // 020-010 : acquire local lock
            // local lock to ensure only one thread in process attempts to acquire the distributed lock and fetch from DB
            await _localLock.WaitAsync();
            try
            {
                // 020-020 : Double-check(maybe another thread in same process has already fetched the data and populated the cache
                // while we were waiting for the local lock)
                string? productsFromCache = await _distributedCache.GetStringAsync(ProductCacheKeys.AllProductsKey);
                if (productsFromCache != null)
                {
                    activity?.SetTag("cache.hit_source", "local_lock_wait");
                    activity?.AddEvent(new("Cache populated by another thread while waiting for local lock."));
                    _logger.LogInformation("Cache populated by another thread while waiting for local lock.");
                    return JsonSerializer.Deserialize<RedisDataWrapper<List<ProductResponse>>>(productsFromCache)!.Data;
                }

                // 020-030 : acquire distributed lock
                // to ensure only one instance in the distributed system fetches from DB and populates cache
                var lockKey = $"lock:{ProductCacheKeys.AllProductsKey}";
                var myLock = _lockProvider.CreateLock(lockKey);

                try
                {
                    activity?.AddEvent(new("Attempting to acquire distributed lock"));

                    // AcquireAsync will throw TimeoutException after timeout
                    await using (var handle = await myLock.AcquireAsync(TimeSpan.FromSeconds(5)))
                    {
                        activity?.SetTag("lock.distributed.acquired", true);

                        // double-check (after acquired distributed lock):
                        // to avoid in case another instance has already fetched data and populated cache
                        // while we were waiting for the distributed lock
                        productsFromCache = await _distributedCache.GetStringAsync(ProductCacheKeys.AllProductsKey);
                        if (productsFromCache != null)
                        {
                            activity?.SetTag("cache.hit_source", "distributed_lock_wait");
                            activity?.AddEvent(new("Cache populated by another instance while waiting for distributed lock."));
                            _logger.LogInformation("Cache populated by another instance while waiting for distributed lock.");
                            return JsonSerializer.Deserialize<RedisDataWrapper<List<ProductResponse>>>(productsFromCache)!.Data;
                        }

                        activity?.SetTag("cache.fill_action", "database_fetch");
                        _logger.LogInformation("Fetching from Database and updating Redis...");

                        //call innerService to fetch data from database
                        var data = await _innerService.GetProductsAsync();
                        var dataList = data.ToList();

                        // 020-040 : populate cache
                        await SaveToCache(ProductCacheKeys.AllProductsKey, dataList);

                        return dataList;
                    }
                }
                catch (TimeoutException ex)
                {
                    _logger.LogError(ex, "Distributed lock timeout for {CacheKey} during cold start.", ProductCacheKeys.AllProductsKey);
                    activity?.SetTag("lock.distributed.timeout", true);
                    throw new Exception("System busy during cold start. Please try again in a moment.", ex);
                }
            }
            finally
            { 
                _localLock.Release();
            }
        }

        private async Task BackgroundRefresh(ActivityContext parentContext)
        {
            using var bgActivity = DiagnosticsConfig.Source.StartActivity(
                "BackgroundProductRefresh",
                ActivityKind.Internal,
                parentContext);

            //to avoid accessing scoped services(ProductsGetterService) that may have been disposed when BackgroundRefresh runs,
            //create a new scope and resolve a new instance of the inner service for background refresh
            using var scope = _scopeFactory.CreateScope();

            try
            {
                bgActivity?.SetTag("cache.key", ProductCacheKeys.AllProductsKey);
                bgActivity?.SetTag("refresh.reason", "logical_expiration");
                _logger.LogInformation("Background refresh started for {CacheKey}", ProductCacheKeys.AllProductsKey);

                // 030-032:acquire distributed lock
                // (prevent other threads in other processes/containers in clusters from fetcing data from db
                // and refreshing cache at the same time.)
                var lockKey = $"lock:{ProductCacheKeys.AllProductsKey}";
                bgActivity?.AddEvent(new("Attempting to acquire RedLock"));

                var myLock = _lockProvider.CreateLock(lockKey);
                // TimeSpan.Zero:try acquire lock immediately, if not acquired, return null immediately without waiting
                await using (var handle = await myLock.TryAcquireAsync(TimeSpan.Zero))
                {
                    if (handle != null)
                    {
                        bgActivity?.SetTag("lock.acquired", true);
                        _logger.LogInformation("Acquired RedLock, fetching fresh data from DB.");

                        // get instance of ProductsGetterService and call GetProductsAsync
                        var scopedInner = scope.ServiceProvider.GetRequiredService<ProductsGetterService>();
                        var freshData = await scopedInner.GetProductsAsync();

                        // update cache with fresh data
                        await SaveToCache(ProductCacheKeys.AllProductsKey, freshData.ToList());

                        bgActivity?.SetStatus(ActivityStatusCode.Ok, "Cache refreshed successfully");
                    }
                    else
                    {
                        bgActivity?.SetTag("lock.acquired", false);
                        bgActivity?.SetTag("cache.status", "skipped_by_lock");
                        bgActivity?.AddEvent(new("DistributedLock acquisition failed - another instance is already refreshing"));
                        _logger.LogInformation("Background refresh skipped: Another instance is already refreshing.");
                    }
                }
            }
            catch (Exception ex)
            {
                bgActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                bgActivity?.AddException(ex);
                _logger.LogError(ex, "Background refresh failed for {CacheKey}", ProductCacheKeys.AllProductsKey);
            }
            finally
            {
                _localLock.Release();
                _logger.LogDebug("Local lock released after background refresh attempt.");
            }
        }

        /// <summary>
        /// 020-040 : populate cache
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private async Task SaveToCache<T>(string key, T data)
        {
            try
            {
                _logger.LogInformation("Saving data to cache for key: {CacheKey}", key);

                // // 020-041 : wrap data with logical expire time
                var wrapper = new RedisDataWrapper<T>
                {
                    Data = data,
                    // set logical expire time to 5 minutes later
                    // can be adjusted based on expected DB fetch time and acceptable staleness
                    LogicExpireTime = DateTime.Now.AddMinutes(_cacheOptions.DefaultExpirationMinutes)
                };

                // 020-042 : store in Redis with absoluteExpiration(24h) much longer than logical expire time
                // to ensure data is not evicted before logical expire time and to allow stale data serving during cache stampede
                var options = new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(24));

                string json = JsonSerializer.Serialize(wrapper, _jsonOptions);

                // update cache
                await _distributedCache.SetStringAsync(key, json, options);

                _logger.LogInformation("Data successfully persisted to Redis for key: {CacheKey}", key);

                Activity.Current?.AddEvent(new("Cache Update Success"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save data to cache for key: {CacheKey}. Continuing without cache update.", key);
                Activity.Current?.AddException(ex);
            }
        }

        #endregion
    }
}
