using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductsMicroservice.Infrastructure.DbContext;
using ProductsMicroservice.Infrastructure.Messaging.Outbox;

namespace ProductsServiceUnitTests;

public sealed class ProductOperationOutboxStoreTests
{
    [Fact]
    public async Task ClaimBatchAsync_ShouldOnlyClaimDueUnlockedEntries()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        DateTime now = DateTime.UtcNow;
        ProductOperationOutbox due = CreateOutbox(now.AddMinutes(-3));
        ProductOperationOutbox future = CreateOutbox(now.AddMinutes(-2));
        future.NextAttemptAtUtc = now.AddMinutes(5);
        ProductOperationOutbox locked = CreateOutbox(now.AddMinutes(-1));
        locked.LockedUntilUtc = now.AddMinutes(5);
        await SeedAsync(database, due, future, locked);
        var store = new ProductOperationOutboxStore(database.CreateScopeFactory());

        IReadOnlyList<ProductOperationOutbox> claimed = await store.ClaimBatchAsync(
            "worker-1", 10, TimeSpan.FromSeconds(30));

        claimed.Should().ContainSingle(entry => entry.NotificationId == due.NotificationId);
        claimed[0].Version.Should().Be(1);
        await using ApplicationDbContext verificationContext = database.CreateContext();
        ProductOperationOutbox persisted = await verificationContext.ProductOperationOutbox
            .SingleAsync(entry => entry.NotificationId == due.NotificationId);
        persisted.LockedBy.Should().Be("worker-1");
        persisted.LockedUntilUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ClaimBatchAsync_WhenWorkersCompete_ShouldOnlyReturnEntryToFirstWorker()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        ProductOperationOutbox outbox = CreateOutbox(DateTime.UtcNow.AddMinutes(-1));
        await SeedAsync(database, outbox);
        var firstStore = new ProductOperationOutboxStore(database.CreateScopeFactory());
        var secondStore = new ProductOperationOutboxStore(database.CreateScopeFactory());

        IReadOnlyList<ProductOperationOutbox> firstClaim = await firstStore.ClaimBatchAsync(
            "worker-1", 1, TimeSpan.FromSeconds(30));
        IReadOnlyList<ProductOperationOutbox> secondClaim = await secondStore.ClaimBatchAsync(
            "worker-2", 1, TimeSpan.FromSeconds(30));

        firstClaim.Should().ContainSingle();
        secondClaim.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkPublishedAsync_ShouldRequireMatchingWorkerAndVersion()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        ProductOperationOutbox outbox = CreateOutbox(DateTime.UtcNow.AddMinutes(-1));
        await SeedAsync(database, outbox);
        var store = new ProductOperationOutboxStore(database.CreateScopeFactory());
        ProductOperationOutbox claimed = (await store.ClaimBatchAsync(
            "worker-1", 1, TimeSpan.FromSeconds(30))).Single();

        bool wrongWorker = await store.MarkPublishedAsync(claimed, "worker-2");
        bool correctWorker = await store.MarkPublishedAsync(claimed, "worker-1");

        wrongWorker.Should().BeFalse();
        correctWorker.Should().BeTrue();
        await using ApplicationDbContext verificationContext = database.CreateContext();
        ProductOperationOutbox persisted = await verificationContext.ProductOperationOutbox.SingleAsync();
        persisted.PublishedAtUtc.Should().NotBeNull();
        persisted.LockedBy.Should().BeNull();
        persisted.Version.Should().Be(2);
    }

    [Fact]
    public async Task ScheduleRetryAsync_ShouldRequireMatchingWorkerAndVersion()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        ProductOperationOutbox outbox = CreateOutbox(DateTime.UtcNow.AddMinutes(-1));
        await SeedAsync(database, outbox);
        var store = new ProductOperationOutboxStore(database.CreateScopeFactory());
        ProductOperationOutbox claimed = (await store.ClaimBatchAsync(
            "worker-1", 1, TimeSpan.FromSeconds(30))).Single();

        await store.ScheduleRetryAsync(
            claimed, "worker-2", 1, TimeSpan.FromMinutes(1), "ignored");
        await store.ScheduleRetryAsync(
            claimed, "worker-1", 1, TimeSpan.FromMinutes(1), "failed");

        await using ApplicationDbContext verificationContext = database.CreateContext();
        ProductOperationOutbox persisted = await verificationContext.ProductOperationOutbox.SingleAsync();
        persisted.AttemptCount.Should().Be(1);
        persisted.LastError.Should().Be("failed");
        persisted.NextAttemptAtUtc.Should().NotBeNull();
        persisted.LockedBy.Should().BeNull();
        persisted.Version.Should().Be(2);
    }

    [Fact]
    public async Task ClaimBatchAsync_WhenCancelled_ShouldPropagateCancellation()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        var store = new ProductOperationOutboxStore(database.CreateScopeFactory());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        Func<Task> action = async () => await store.ClaimBatchAsync(
            "worker-1", 1, TimeSpan.FromSeconds(30), cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static async Task SeedAsync(
        SqliteProductsTestDatabase database,
        params ProductOperationOutbox[] entries)
    {
        await using ApplicationDbContext dbContext = database.CreateContext();
        dbContext.ProductOperationOutbox.AddRange(entries);
        await dbContext.SaveChangesAsync();
    }

    private static ProductOperationOutbox CreateOutbox(DateTime createdAtUtc) => new()
    {
        NotificationId = Guid.NewGuid(),
        OccurredAtUtc = createdAtUtc,
        Payload = "{}",
        PayloadHash = new string('A', 64),
        CreatedAtUtc = createdAtUtc
    };
}
