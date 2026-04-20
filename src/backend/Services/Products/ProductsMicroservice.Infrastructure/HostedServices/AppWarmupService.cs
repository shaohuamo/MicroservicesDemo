using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.Diagnostics;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Services;
using ProductsMicroservice.Infrastructure.DbContext;
using ProductsMicroservice.Infrastructure.Options;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace ProductsMicroservice.Infrastructure.HostedServices
{
    public class AppWarmupService : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AppWarmupService> _logger;
        private readonly CacheOptions _cacheOptions;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        //Telemetry
        public const string ServiceName = "Product.Api.Warmup";
        private static readonly ActivitySource ActivitySource = new(ServiceName);
        public static readonly Meter WarmupMeter = new(ServiceName);
        private static readonly Gauge<double> WarmupDuration = 
            WarmupMeter.CreateGauge<double>("product.api.warmup.duration", "s");
        private static readonly Gauge<long> ServiceStartTime =
            WarmupMeter.CreateGauge<long>("product.api.start.time.seconds", "s");

        public AppWarmupService(IServiceScopeFactory scopeFactory, ILogger<AppWarmupService> logger,
            IOptions<CacheOptions> cacheOptions)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _cacheOptions = cacheOptions.Value;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            ServiceStartTime.Record(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            using var activity = ActivitySource.StartActivity("GlobalWarmup");
            var sw = Stopwatch.StartNew();

            _logger.LogInformation("GlobalWarmup Starting...");

            string status = "success";
            try
            {
                //Warmup logic
                await PreheatDatabaseAsync(ct);
                await PreheatCacheAsync(ct);

                sw.Stop();
                _logger.LogInformation("GlobalWarmup successful, Total Elapsed: {Elapsed}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogCritical(ex, "GlobalWarmup failed, error occur during GlobalWarmup，Elapsed: {Elapsed}ms", 
                    sw.ElapsedMilliseconds);
                status = "error";
                throw;
            }
            finally
            {

                WarmupDuration.Record(sw.Elapsed.TotalSeconds,new KeyValuePair<string, object?>("status", status));
            }
        }

        private async Task PreheatDatabaseAsync(CancellationToken ct)
        {
            using var activity = ActivitySource.StartActivity("DatabaseWarmup");
            var sw = Stopwatch.StartNew();

            _logger.LogInformation("Database Warmup Starting...");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // 010-000: check connection
                if (!await dbContext.Database.CanConnectAsync(ct))
                {
                    throw new InvalidOperationException("Can not connect to Database, Warmup failed");
                }

                // 020-000: trigger EF Core model cache initialization
                _ = await dbContext.Products.AsNoTracking().AnyAsync(ct);

                sw.Stop();
                _logger.LogInformation("Database Warmup successful , Elapsed: {Elapsed}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                sw.Stop();
                activity?.SetStatus(ActivityStatusCode.Error, e.Message);
                _logger.LogError(e, "Error occur during Database Warmup, Elapsed: {Elapsed}ms", sw.ElapsedMilliseconds);

                throw;
            }
        }

        private async Task PreheatCacheAsync(CancellationToken ct)
        {
            using var activity = ActivitySource.StartActivity("CacheWarmup");
            using var scope = _scopeFactory.CreateScope();
            var sw = Stopwatch.StartNew();

            var productsGetterService = scope.ServiceProvider.GetRequiredService<ProductsGetterService>();
            var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

            _logger.LogInformation("Products Cache Starting: {Key}", ProductCacheKeys.AllProductsKey);

            try
            {
                // 010-000：check cache is existed or not
                var existingJson = await cache.GetStringAsync(ProductCacheKeys.AllProductsKey, ct);
                if (existingJson != null)
                {
                    sw.Stop();
                    _logger.LogInformation("Products has been cached, skip cache warmup, Elapsed: {Elapsed}ms", 
                        sw.ElapsedMilliseconds);
                    //trigger deserialization to warmup JsonSerializer's internal cache
                    JsonSerializer.Deserialize<RedisDataWrapper<List<ProductResponse?>>>(existingJson, JsonOptions);
                    return;
                }

                // 020-000: fetch data from database
                _logger.LogInformation("Products cahce not exist, featch data from database");
                var data = await productsGetterService.GetProductsAsync();
                var dataList = data.ToList();

                if (dataList.Count == 0)
                {
                    sw.Stop();
                    _logger.LogWarning("There are no Products in database,skip cache warmup, Elapsed: {Elapsed}ms",
                        sw.ElapsedMilliseconds);
                    return;
                }

                // 020-010: record cache warmup metrics
                DiagnosticsConfig.ProductsCounter.Add(dataList.Count,
                    new KeyValuePair<string, object?>("status", "success"));

                // 030-000:cache products with logical expiration
                var wrapper = new RedisDataWrapper<List<ProductResponse?>>
                {
                    Data = dataList,
                    LogicExpireTime = DateTime.Now.AddMinutes(_cacheOptions.DefaultExpirationMinutes)
                };

                var json = JsonSerializer.Serialize(wrapper, JsonOptions);

                // set absolute expiration to prevent cache avalanche, logical expiration will handle data freshness
                var options = new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(24));

                await cache.SetStringAsync(ProductCacheKeys.AllProductsKey, json, options, ct);

                sw.Stop();
                _logger.LogInformation("Products cache successful, Elapsed: {Elapsed}ms，contains {Count} rows data。",
                    sw.ElapsedMilliseconds, dataList.Count);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Error occur during cache warmup: {Key}, Elapsed: {Elapsed}ms",
                    sw.ElapsedMilliseconds, ProductCacheKeys.AllProductsKey);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
