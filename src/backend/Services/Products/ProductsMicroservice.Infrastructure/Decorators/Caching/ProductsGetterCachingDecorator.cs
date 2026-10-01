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
using ProductsMicroservice.Infrastructure.Redis;
using System.Diagnostics;
using System.Text.Json;

namespace ProductsMicroservice.Infrastructure.Decorators.Caching
{
    public class ProductsGetterCachingDecorator : IProductsGetterService
    {
        private readonly IProductsGetterService _innerService;
        private readonly IDistributedCache _distributedCache;
        private readonly IProductsRedisConnectionProvider _connections;
        private readonly IProductsRedisLockFactory _lockFactory;
        private readonly CacheOptions _cacheOptions;
        private readonly ILogger<ProductsGetterCachingDecorator> _logger;
        private static readonly SemaphoreSlim _localLock = new(1, 1);
        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IServiceScopeFactory _scopeFactory;

        public ProductsGetterCachingDecorator(
            IProductsGetterService inner, IDistributedCache cache,
            IProductsRedisConnectionProvider connections, IProductsRedisLockFactory lockFactory,
            IOptions<CacheOptions> options,
            ILogger<ProductsGetterCachingDecorator> logger, IServiceScopeFactory scopeFactory)
        {
            _innerService = inner;
            _distributedCache = cache;
            _connections = connections;
            _lockFactory = lockFactory;
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
            var (cacheAvailable, cachedData) = await TryReadCacheAsync(cacheKey, "detail_read");
            if (!cacheAvailable)
            {
                return await _innerService.GetProductByProductIdAsync(productId);
            }

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

                try
                {
                    var cachedProduct = JsonSerializer.Deserialize<ProductResponse>(cachedData, _jsonOptions);
                    if (cachedProduct is not null)
                    {
                        activity?.SetTag("cache.hit_type", "data");
                        _logger.LogInformation("Cache Hit for ProductId: {ProductId}", productId);
                        return cachedProduct;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Invalid detail cache value for {CacheKey}", cacheKey);
                }
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

                await TryWriteCacheAsync(cacheKey, _cacheOptions.NullValuePlaceholder, negativeOptions);
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

            await TryWriteCacheAsync(cacheKey, JsonSerializer.Serialize(result), cacheOptions);

            _logger.LogInformation("Product retrieved from database; cache fill attempted.");
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
            var (cacheAvailable, productsFromCache) = await TryReadCacheAsync(
                ProductCacheKeys.AllProductsKey, "list_read");
            if (!cacheAvailable)
            {
                return await _innerService.GetProductsAsync();
            }

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
            var productsFromCache = ParseListCache(cachedProducts);

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
                var (cacheAvailable, productsFromCache) = await TryReadCacheAsync(
                    ProductCacheKeys.AllProductsKey, "list_local_recheck");
                if (!cacheAvailable)
                {
                    return await _innerService.GetProductsAsync();
                }

                var cached = productsFromCache is null ? null : ParseListCache(productsFromCache);
                if (cached?.Data is not null)
                {
                    activity?.SetTag("cache.hit_source", "local_lock_wait");
                    activity?.AddEvent(new("Cache populated by another thread while waiting for local lock."));
                    _logger.LogInformation("Cache populated by another thread while waiting for local lock.");
                    return cached.Data;
                }

                // 020-030 : acquire distributed lock
                // to ensure only one instance in the distributed system fetches from DB and populates cache
                var lockKey = $"lock:{ProductCacheKeys.AllProductsKey}";
                IDistributedSynchronizationHandle handle;
                try
                {
                    activity?.AddEvent(new("Attempting to acquire distributed lock"));
                    var myLock = await _lockFactory.CreateLockAsync(lockKey);
                    handle = await myLock.AcquireAsync(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    RecordRedisFailure(ex, "list_lock_acquire");
                    return await _innerService.GetProductsAsync();
                }

                try
                {
                    activity?.SetTag("lock.distributed.acquired", true);
                    var (recheckAvailable, recheckedProducts) = await TryReadCacheAsync(
                        ProductCacheKeys.AllProductsKey, "list_distributed_recheck");
                    if (!recheckAvailable)
                    {
                        return await _innerService.GetProductsAsync();
                    }

                    cached = recheckedProducts is null ? null : ParseListCache(recheckedProducts);
                    if (cached?.Data is not null)
                    {
                        activity?.SetTag("cache.hit_source", "distributed_lock_wait");
                        return cached.Data;
                    }

                    activity?.SetTag("cache.fill_action", "database_fetch");
                    var dataList = (await _innerService.GetProductsAsync()).ToList();
                    await SaveToCache(ProductCacheKeys.AllProductsKey, dataList);
                    return dataList;
                }
                finally
                {
                    try
                    {
                        await handle.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        RecordRedisFailure(ex, "list_lock_release");
                    }
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

            try
            {
                // The request scope may be gone when this task runs.
                using var scope = _scopeFactory.CreateScope();
                bgActivity?.SetTag("cache.key", ProductCacheKeys.AllProductsKey);
                bgActivity?.SetTag("refresh.reason", "logical_expiration");
                _logger.LogInformation("Background refresh started for {CacheKey}", ProductCacheKeys.AllProductsKey);

                // 030-032:acquire distributed lock
                // (prevent other threads in other processes/containers in clusters from fetcing data from db
                // and refreshing cache at the same time.)
                var lockKey = $"lock:{ProductCacheKeys.AllProductsKey}";
                bgActivity?.AddEvent(new("Attempting to acquire RedLock"));

                var myLock = await _lockFactory.CreateLockAsync(lockKey);
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

        private async Task<(bool Available, string? Value)> TryReadCacheAsync(string key, string stage)
        {
            try
            {
                var connection = await _connections.GetConnectionAsync();
                if (!connection.IsConnected)
                {
                    RecordRedisFailure(new InvalidOperationException("Products Redis is disconnected."), stage);
                    return (false, null);
                }

                return (true, await _distributedCache.GetStringAsync(key));
            }
            catch (Exception ex)
            {
                RecordRedisFailure(ex, stage);
                return (false, null);
            }
        }

        private async Task TryWriteCacheAsync(
            string key, string value, DistributedCacheEntryOptions options)
        {
            try
            {
                var connection = await _connections.GetConnectionAsync();
                if (!connection.IsConnected)
                {
                    RecordRedisFailure(new InvalidOperationException("Products Redis is disconnected."),
                        "detail_write", fallback: false);
                    return;
                }

                await _distributedCache.SetStringAsync(key, value, options);
            }
            catch (Exception ex)
            {
                RecordRedisFailure(ex, "detail_write", fallback: false);
            }
        }

        private RedisDataWrapper<List<ProductResponse>>? ParseListCache(string value)
        {
            try
            {
                return JsonSerializer.Deserialize<RedisDataWrapper<List<ProductResponse>>>(value, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid all-products cache value");
                return null;
            }
        }

        private void RecordRedisFailure(Exception exception, string stage, bool fallback = true)
        {
            bool lockTimeout = exception is TimeoutException && stage == "list_lock_acquire";
            _logger.LogWarning(exception, "Products cache operation failed at {Stage}", stage);
            var activity = Activity.Current;
            activity?.AddException(exception);
            activity?.SetTag("cache.failure_stage", stage);
            if (fallback)
            {
                activity?.SetTag("cache.status", lockTimeout ? "lock_timeout" : "redis_unavailable");
                activity?.SetTag("cache.fallback", "database");
                DiagnosticsConfig.CacheReadFallbackCounter.Add(1,
                    new KeyValuePair<string, object?>("stage", stage),
                    new KeyValuePair<string, object?>("read", stage.StartsWith("detail", StringComparison.Ordinal)
                        ? "detail" : "list"));
            }
        }

        #endregion
    }
}
