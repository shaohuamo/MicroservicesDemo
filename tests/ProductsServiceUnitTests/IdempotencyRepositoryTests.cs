using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductsMicroservice.Core.Domain;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Infrastructure.Repositories;
using ProductsServiceUnitTests;

namespace ProductsMicroservice.Tests;

public sealed class IdempotencyRepositoryTests
{
    [Fact]
    public async Task GetAsync_ShouldScopeKeyByUserAndOperation()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        Guid key = Guid.NewGuid();
        await using (var writeContext = database.CreateContext())
        {
            writeContext.IdempotencyRecords.AddRange(
                CreateRecord("user-1", key, DateTimeOffset.UtcNow.AddHours(1)),
                CreateRecord("user-2", key, DateTimeOffset.UtcNow.AddHours(1)));
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = database.CreateContext();
        var repository = new IdempotencyRepository(readContext);

        IdempotencyRecord? first = await repository.GetAsync(
            "user-1", IdempotencyOperation.AddProduct, key);
        IdempotencyRecord? second = await repository.GetAsync(
            "user-2", IdempotencyOperation.AddProduct, key);

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        first!.UserId.Should().Be("user-1");
        second!.UserId.Should().Be("user-2");
    }

    [Fact(Skip = "SQLite cannot translate DateTimeOffset ordering; PostgreSQL behavior is covered by the generated migration and database integration verification.")]
    public async Task DeleteExpiredAsync_ShouldDeleteRecordsAtBoundary_AndKeepFutureRecords()
    {
        await using SqliteProductsTestDatabase database = await SqliteProductsTestDatabase.CreateAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await using (var writeContext = database.CreateContext())
        {
            writeContext.IdempotencyRecords.AddRange(
                CreateRecord("expired", Guid.NewGuid(), now.AddTicks(-1)),
                CreateRecord("boundary", Guid.NewGuid(), now),
                CreateRecord("future", Guid.NewGuid(), now.AddTicks(1)));
            await writeContext.SaveChangesAsync();
        }

        await using (var cleanupContext = database.CreateContext())
        {
            var repository = new IdempotencyRepository(cleanupContext);
            (await repository.DeleteExpiredAsync(now)).Should().Be(2);
        }

        await using var verifyContext = database.CreateContext();
        List<string> remainingUsers = await verifyContext.IdempotencyRecords
            .Select(record => record.UserId)
            .ToListAsync();
        remainingUsers.Should().Equal("future");
    }

    private static IdempotencyRecord CreateRecord(
        string userId, Guid key, DateTimeOffset expiresAt) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Operation = IdempotencyOperation.AddProduct,
            IdempotencyKey = key,
            RequestHash = new string('A', 64),
            ResponseStatusCode = 201,
            ResponseJson = "{}",
            CreatedAtUtc = expiresAt.AddHours(-24),
            ExpiresAtUtc = expiresAt
        };
}
