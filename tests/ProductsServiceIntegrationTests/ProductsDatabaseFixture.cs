using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProductsMicroservice.Core.Mappers;
using ProductsMicroservice.Infrastructure.DbContext;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace ProductsServiceIntegrationTests;

public sealed class ProductsDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    internal readonly RedisContainer Redis = new RedisBuilder("redis:7-alpine").Build();
    private IConnectionMultiplexer _connection = null!;
    internal RedisCache Cache { get; private set; } = null!;
    internal IMapper Mapper { get; } = new MapperConfiguration(configuration =>
    {
        configuration.AddProfile<ProductAddRequestToProductMappingProfile>();
        configuration.AddProfile<ProductUpdateRequestToProductMappingProfile>();
        configuration.AddProfile<ProductToProductResponseMappingProfile>();
    }, NullLoggerFactory.Instance).CreateMapper();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), Redis.StartAsync());
        await ConnectCacheAsync();
        await using ApplicationDbContext context = CreateContext();
        await context.Database.MigrateAsync();
    }

    internal async Task ConnectCacheAsync()
    {
        Cache?.Dispose();
        if (_connection is not null) await _connection.DisposeAsync();
        ConfigurationOptions configuration = ConfigurationOptions.Parse(Redis.GetConnectionString());
        configuration.AbortOnConnectFail = false;
        configuration.AsyncTimeout = 500;
        configuration.BacklogPolicy = BacklogPolicy.FailFast;
        _connection = await ConnectionMultiplexer.ConnectAsync(configuration);
        Cache = new RedisCache(Options.Create(new RedisCacheOptions
        {
            InstanceName = "integration:",
            ConnectionMultiplexerFactory = () => Task.FromResult(_connection)
        }));
    }

    internal ApplicationDbContext CreateContext(SaveChangesInterceptor? interceptor = null, bool unavailable = false)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(unavailable
            ? "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=test;Timeout=1"
            : _postgres.GetConnectionString());
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        return new ApplicationDbContext(builder.Options);
    }

    internal Task<TimeSpan?> GetTtlAsync(string key) =>
        _connection.GetDatabase().KeyTimeToLiveAsync("integration:" + key);

    public async Task DisposeAsync()
    {
        Cache?.Dispose();
        if (_connection is not null) await _connection.DisposeAsync();
        await Redis.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
