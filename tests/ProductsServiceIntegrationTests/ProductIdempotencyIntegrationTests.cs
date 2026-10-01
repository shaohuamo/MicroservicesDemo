using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProductsMicroservice.Core.Domain;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Options;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Core.Services;
using ProductsMicroservice.Infrastructure.DbContext;
using ProductsMicroservice.Infrastructure.Decorators.Idempotency;
using ProductsMicroservice.Infrastructure.Messaging;
using ProductsMicroservice.Infrastructure.Repositories;

namespace ProductsServiceIntegrationTests;

public sealed class ProductIdempotencyIntegrationTests(ProductsDatabaseFixture database)
    : IClassFixture<ProductsDatabaseFixture>
{
    [Theory]
    [InlineData(IdempotencyOperation.AddProduct)]
    [InlineData(IdempotencyOperation.UpdateProduct)]
    [InlineData(IdempotencyOperation.DeleteProduct)]
    public async Task SameKeyConcurrentRequests_ShouldCommitOneProductMutationOutboxAndResult(IdempotencyOperation operation)
    {
        string user = Guid.NewGuid().ToString();
        Guid key = Guid.NewGuid();
        Guid productId = operation == IdempotencyOperation.AddProduct ? Guid.Empty : await SeedProductAsync();
        string name = Guid.NewGuid().ToString();
        var barrier = new CommitBarrier();
        await using ApplicationDbContext firstContext = database.CreateContext(barrier);
        await using ApplicationDbContext secondContext = database.CreateContext(barrier);

        Result[] results = await Task.WhenAll(
            InvokeAsync(firstContext, operation, user, key, productId, name),
            InvokeAsync(secondContext, operation, user, key, productId, name));

        results.Count(result => !result.IsReplay).Should().Be(1);
        results[0].Product.Should().BeEquivalentTo(results[1].Product);
        await using ApplicationDbContext verify = database.CreateContext();
        (await verify.IdempotencyRecords.CountAsync(record => record.UserId == user)).Should().Be(1);
        Guid affectedId = results[0].Product?.ProductId ?? productId;
        (await verify.ProductOperationOutbox.Select(record => record.Payload).ToListAsync())
            .Count(payload => payload.Contains(affectedId.ToString())).Should().Be(1);
        Product? product = await verify.Products.FindAsync(affectedId);
        if (operation == IdempotencyOperation.DeleteProduct) product.Should().BeNull();
        else product!.Version.Should().Be(operation == IdempotencyOperation.AddProduct ? 1 : 2);
        // The losing transaction must discard every staged entity before rereading the winner.
        var loser = results[0].IsReplay ? firstContext : secondContext;
        loser.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Theory]
    [InlineData(IdempotencyOperation.AddProduct)]
    [InlineData(IdempotencyOperation.UpdateProduct)]
    [InlineData(IdempotencyOperation.DeleteProduct)]
    public async Task RedisReplay_ShouldSucceedWithDatabaseUnavailable_AndEvictionShouldRefillFromDatabase(IdempotencyOperation operation)
    {
        string user = Guid.NewGuid().ToString();
        Guid key = Guid.NewGuid();
        Guid productId = operation == IdempotencyOperation.AddProduct ? Guid.Empty : await SeedProductAsync();
        string name = Guid.NewGuid().ToString();
        await using ApplicationDbContext initial = database.CreateContext();
        Result first = await InvokeAsync(initial, operation, user, key, productId, name);
        string cacheKey = ProductIdempotencyResultCache.CreateKey(user, operation, key);
        TimeSpan? originalTtl = await database.GetTtlAsync(cacheKey);
        originalTtl.Should().BePositive();
        await using ApplicationDbContext unavailable = database.CreateContext(unavailable: true);
        Result cached = await InvokeAsync(unavailable, operation, user, key, productId, name);
        cached.Source.Should().Be(IdempotencyResultSource.Redis);
        cached.Product.Should().BeEquivalentTo(first.Product);

        await database.Cache.RemoveAsync(cacheKey);
        // RedisCache uses whole-second TTLs; cross that boundary to detect accidental renewal.
        await Task.Delay(1100);
        await using ApplicationDbContext retry = database.CreateContext();
        Result refilled = await InvokeAsync(retry, operation, user, key, productId, name);
        refilled.Source.Should().Be(IdempotencyResultSource.Database);
        (await database.GetTtlAsync(cacheKey)).Should().BeLessThanOrEqualTo(originalTtl!.Value);
        refilled.Product.Should().BeEquivalentTo(first.Product);
    }

    [Fact]
    public async Task RedisUnavailable_ShouldCommitAndReplayFromDatabase_ThenRecoverCache()
    {
        string user = Guid.NewGuid().ToString();
        Guid key = Guid.NewGuid();
        string name = Guid.NewGuid().ToString();
        await database.Redis.StopAsync();
        try
        {
            await using ApplicationDbContext first = database.CreateContext();
            Result success = await InvokeAsync(first, IdempotencyOperation.AddProduct, user, key, Guid.Empty, name);
            success.IsReplay.Should().BeFalse();
            await using ApplicationDbContext retry = database.CreateContext();
            Result replay = await InvokeAsync(retry, IdempotencyOperation.AddProduct, user, key, Guid.Empty, name);
            replay.Source.Should().Be(IdempotencyResultSource.Database);
            replay.Product.Should().BeEquivalentTo(success.Product);
        }
        finally
        {
            await database.Redis.StartAsync();
            // Testcontainers may publish a new random host port when the container restarts.
            await database.ConnectCacheAsync();
        }
        await using ApplicationDbContext recovered = database.CreateContext();
        await InvokeAsync(recovered, IdempotencyOperation.AddProduct, user, key, Guid.Empty, name);
        string cacheKey = ProductIdempotencyResultCache.CreateKey(user, IdempotencyOperation.AddProduct, key);
        (await database.Cache.GetStringAsync(cacheKey)).Should().NotBeNull();
    }

    [Theory]
    [InlineData(IdempotencyOperation.AddProduct)]
    [InlineData(IdempotencyOperation.UpdateProduct)]
    [InlineData(IdempotencyOperation.DeleteProduct)]
    public async Task FailedOutboxInsert_ShouldRollBackProductAndIdempotencyResult(IdempotencyOperation operation)
    {
        string user = Guid.NewGuid().ToString();
        Guid key = Guid.NewGuid();
        Guid productId = operation == IdempotencyOperation.AddProduct ? Guid.Empty : await SeedProductAsync();
        string name = Guid.NewGuid().ToString();
        await using ApplicationDbContext context = database.CreateContext();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_fail_outbox() RETURNS trigger LANGUAGE plpgsql AS
            $$ BEGIN RAISE EXCEPTION 'injected outbox failure'; END $$;
            CREATE TRIGGER test_fail_outbox BEFORE INSERT ON "ProductOperationOutbox"
                FOR EACH ROW EXECUTE FUNCTION test_fail_outbox();
            """);
        try
        {
            await FluentActions.Invoking(() => InvokeAsync(context, operation, user, key, productId, name))
                .Should().ThrowAsync<DbUpdateException>();
            context.ChangeTracker.Entries().Should().BeEmpty();
            await using ApplicationDbContext verify = database.CreateContext();
            (await verify.IdempotencyRecords.AnyAsync(record => record.UserId == user)).Should().BeFalse();
            (await verify.ProductOperationOutbox.Select(record => record.Payload).ToListAsync())
                .Should().NotContain(payload => payload.Contains(user));
            (await database.Cache.GetStringAsync(ProductIdempotencyResultCache.CreateKey(user, operation, key)))
                .Should().BeNull();
            if (operation == IdempotencyOperation.AddProduct)
                (await verify.Products.AnyAsync(product => product.DisplayName == name)).Should().BeFalse();
            else (await verify.Products.FindAsync(productId))!.Version.Should().Be(1);
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER test_fail_outbox ON "ProductOperationOutbox";
                DROP FUNCTION test_fail_outbox();
                """);
        }
    }

    [Fact]
    public async Task DifferentPayloadWithSameKey_ShouldConflictAfterUniqueConstraintRace()
    {
        string user = Guid.NewGuid().ToString();
        Guid key = Guid.NewGuid();
        var barrier = new CommitBarrier();
        await using ApplicationDbContext first = database.CreateContext(barrier);
        await using ApplicationDbContext second = database.CreateContext(barrier);
        async Task<Exception?> Attempt(ApplicationDbContext context, string name)
        {
            try { await InvokeAsync(context, IdempotencyOperation.AddProduct, user, key, Guid.Empty, name); return null; }
            catch (Exception exception) { return exception; }
        }
        Exception?[] results = await Task.WhenAll(Attempt(first, Guid.NewGuid().ToString()), Attempt(second, Guid.NewGuid().ToString()));
        results.Should().ContainSingle(exception => exception == null);
        results.Single(exception => exception is not null).Should().BeOfType<IdempotencyPayloadConflictException>();
        await using ApplicationDbContext verify = database.CreateContext();
        (await verify.IdempotencyRecords.CountAsync(record => record.UserId == user)).Should().Be(1);
        (await verify.ProductOperationOutbox.Select(record => record.Payload).ToListAsync())
            .Count(payload => payload.Contains(user)).Should().Be(1);
    }

    private async Task<Guid> SeedProductAsync()
    {
        await using ApplicationDbContext context = database.CreateContext();
        string name = Guid.NewGuid().ToString();
        var product = new Product { ProductId = Guid.NewGuid(), DisplayName = name, ProductName = name.ToUpperInvariant(), UnitPrice = 10, QuantityInStock = 2, Version = 1 };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product.ProductId;
    }

    [Fact]
    public async Task ExpiredResult_ShouldReplayUntilDatabaseCleanup_WithoutRefillingRedis()
    {
        string user = Guid.NewGuid().ToString();
        Guid key = Guid.NewGuid();
        Guid productId = await SeedProductAsync();
        string name = Guid.NewGuid().ToString();
        await using ApplicationDbContext context = database.CreateContext();
        await InvokeAsync(context, IdempotencyOperation.UpdateProduct, user, key, productId, name);
        await context.IdempotencyRecords.Where(record => record.UserId == user)
            .ExecuteUpdateAsync(update => update.SetProperty(record => record.ExpiresAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
        string cacheKey = ProductIdempotencyResultCache.CreateKey(user, IdempotencyOperation.UpdateProduct, key);
        await database.Cache.RemoveAsync(cacheKey);
        await using ApplicationDbContext retry = database.CreateContext();

        (await InvokeAsync(retry, IdempotencyOperation.UpdateProduct, user, key, productId, name))
            .Source.Should().Be(IdempotencyResultSource.Database);
        (await database.Cache.GetStringAsync(cacheKey)).Should().BeNull();
        (await new IdempotencyRepository(retry).DeleteExpiredAsync(DateTimeOffset.UtcNow)).Should().Be(1);
        await FluentActions.Invoking(() => InvokeAsync(retry, IdempotencyOperation.UpdateProduct, user, key, productId, name))
            .Should().ThrowAsync<ProductConcurrencyException>();
        (await retry.IdempotencyRecords.AnyAsync(record => record.UserId == user)).Should().BeFalse();
    }

    private async Task<Result> InvokeAsync(ApplicationDbContext context, IdempotencyOperation operation,
        string user, Guid key, Guid productId, string name)
    {
        var accessor = new OperationContext(user);
        var repository = new ProductsRepository(context);
        var outbox = new ProductOperationOutboxWriter(context);
        var executor = new ProductIdempotencyExecutor(new IdempotencyRepository(context), context, accessor,
            Options.Create(new IdempotencyOptions()),
            new ProductIdempotencyResultCache(database.Cache, NullLogger<ProductIdempotencyResultCache>.Instance));
        switch (operation)
        {
            case IdempotencyOperation.AddProduct:
                var added = await new ProductsAdderIdempotencyDecorator(new ProductsAdderService(database.Mapper, repository, outbox, accessor), executor)
                    .AddProductAsync(new ProductAddRequest { DisplayName = name, UnitPrice = 10, QuantityInStock = 2 }, key);
                return new(added.IsReplay, added.Source, added.Product);
            case IdempotencyOperation.UpdateProduct:
                var updated = await new ProductsUpdaterIdempotencyDecorator(new ProductsUpdaterService(repository, database.Mapper, outbox, accessor), executor)
                    .UpdateProductAsync(new ProductUpdateRequest { ProductId = productId, DisplayName = name, UnitPrice = 10, QuantityInStock = 2, Version = 1 }, key);
                return new(updated.IsReplay, updated.Source, updated.Product);
            default:
                var deleted = await new ProductsDeleterIdempotencyDecorator(new ProductsDeleterService(repository, outbox, accessor), executor)
                    .DeleteProductAsync(productId, 1, key);
                deleted.Deleted.Should().BeTrue();
                return new(deleted.IsReplay, deleted.Source, null);
        }
    }

    private sealed record Result(bool IsReplay, IdempotencyResultSource Source, ProductResponse? Product);
    private sealed class OperationContext(string user) : IProductOperationContextAccessor
    {
        public ProductOperationContext GetCurrent() => new(user, "test@example.com", "en", "integration");
    }

    private sealed class CommitBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrived) == 2) _ready.TrySetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }
    }
}
