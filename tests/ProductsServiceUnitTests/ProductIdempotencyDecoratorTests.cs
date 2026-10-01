using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProductsMicroservice.Core.Domain;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.Exceptions;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Options;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Idempotency;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProductsMicroservice.Tests;

public sealed class ProductIdempotencyDecoratorTests
{
    private readonly Mock<IIdempotencyRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly Mock<IProductsAdderService> _adder = new();
    private readonly Mock<IProductsUpdaterService> _updater = new();
    private readonly Mock<IProductsDeleterService> _deleter = new();
    private readonly Mock<IProductOperationContextAccessor> _context = new();
    private readonly Dictionary<string, byte[]> _values = new();
    private readonly ProductIdempotencyExecutor _executor;
    private readonly Guid _key = Guid.NewGuid();
    private readonly ProductResponse _product = new(Guid.NewGuid(), "Test", 10, 2, 2);
    private IdempotencyRecord? _pending;
    private IdempotencyRecord? _stored;
    private DistributedCacheEntryOptions? _expiration;
    private string _name = "Test";
    private int _version = 1;

    public ProductIdempotencyDecoratorTests()
    {
        _context.Setup(x => x.GetCurrent()).Returns(new ProductOperationContext("user-1", "user@example.com", "en", "correlation"));
        _cache.Setup(x => x.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync((string key, CancellationToken _) => _values.GetValueOrDefault(key));
        _cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default))
            .Callback((string key, byte[] value, DistributedCacheEntryOptions expiration, CancellationToken _) =>
            {
                _stored.Should().NotBeNull("only committed results may enter Redis");
                _values[key] = value;
                _expiration = expiration;
            }).Returns(Task.CompletedTask);
        _repository.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<IdempotencyOperation>(), _key, default))
            .ReturnsAsync((string user, IdempotencyOperation operation, Guid _, CancellationToken __) =>
                _stored?.UserId == user && _stored.Operation == operation ? _stored : null);
        _repository.Setup(x => x.Add(It.IsAny<IdempotencyRecord>())).Callback<IdempotencyRecord>(record => _pending = record);
        _unitOfWork.Setup(x => x.SaveChangesAsync(default)).Callback(() => _stored = _pending).ReturnsAsync(1);
        _unitOfWork.Setup(x => x.DiscardPendingChanges()).Callback(() => _pending = null);
        _adder.Setup(x => x.AddProductAsync(It.IsAny<ProductAddRequest>(), _key)).ReturnsAsync(new ProductAddResult(_product, false));
        _updater.Setup(x => x.UpdateProductAsync(It.IsAny<ProductUpdateRequest>(), _key)).ReturnsAsync(new ProductUpdateResult(_product, false));
        _deleter.Setup(x => x.DeleteProductAsync(It.IsAny<Guid>(), It.IsAny<int>(), _key)).ReturnsAsync(new ProductDeleteResult(true, false));
        _executor = new ProductIdempotencyExecutor(_repository.Object, _unitOfWork.Object, _context.Object,
            Options.Create(new IdempotencyOptions()),
            new ProductIdempotencyResultCache(_cache.Object, NullLogger<ProductIdempotencyResultCache>.Instance));
    }

    #region Committed results and replay

    [Theory]
    [InlineData(IdempotencyOperation.AddProduct)]
    [InlineData(IdempotencyOperation.UpdateProduct)]
    [InlineData(IdempotencyOperation.DeleteProduct)]
    public async Task Execute_ShouldCommitOnce_AndReplayFromRedisWithoutReadingDatabase(IdempotencyOperation operation)
    {
        var first = await Invoke(operation);
        var replay = await Invoke(operation);

        first.IsReplay.Should().BeFalse();
        first.Source.Should().Be(IdempotencyResultSource.Executed);
        replay.IsReplay.Should().BeTrue();
        replay.Source.Should().Be(IdempotencyResultSource.Redis);
        replay.Product.Should().BeEquivalentTo(first.Product);
        _stored!.Operation.Should().Be(operation);
        _stored.ResponseStatusCode.Should().Be(operation == IdempotencyOperation.AddProduct ? 201 : 200);
        _stored.ExpiresAtUtc.Subtract(_stored.CreatedAtUtc).Should().Be(TimeSpan.FromHours(24));
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
        _repository.Verify(x => x.GetAsync("user-1", operation, _key, default), Times.Once);
        VerifyInner(operation, Times.Once());
        _expiration!.AbsoluteExpiration.Should().Be(_stored.ExpiresAtUtc);
        _expiration.SlidingExpiration.Should().BeNull();
    }

    [Theory]
    [InlineData(IdempotencyOperation.AddProduct)]
    [InlineData(IdempotencyOperation.UpdateProduct)]
    [InlineData(IdempotencyOperation.DeleteProduct)]
    public async Task Execute_ShouldRefillEvictedRedisResult_WithoutExtendingExpiration(IdempotencyOperation operation)
    {
        await Invoke(operation);
        DateTimeOffset expiry = _stored!.ExpiresAtUtc;
        _values.Clear();

        var replay = await Invoke(operation);

        replay.Source.Should().Be(IdempotencyResultSource.Database);
        _values.Should().ContainSingle();
        _expiration!.AbsoluteExpiration.Should().Be(expiry);
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
        VerifyInner(operation, Times.Once());
    }

    [Theory]
    [InlineData(IdempotencyOperation.AddProduct)]
    [InlineData(IdempotencyOperation.UpdateProduct)]
    [InlineData(IdempotencyOperation.DeleteProduct)]
    public async Task Execute_ShouldRejectChangedPayload_UsingRedisWithoutReadingDatabase(IdempotencyOperation operation)
    {
        await Invoke(operation);
        _name = "Changed";
        _version++;

        await FluentActions.Invoking(() => Invoke(operation)).Should().ThrowAsync<IdempotencyPayloadConflictException>();

        _repository.Verify(x => x.GetAsync("user-1", operation, _key, default), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
        VerifyInner(operation, Times.Once());
    }

    [Fact]
    public async Task Execute_ShouldPreserveLegacyAddFingerprint()
    {
        await Invoke(IdempotencyOperation.AddProduct);
        const string canonical = "{\"displayName\":\"Test\",\"unitPrice\":10,\"quantityInStock\":2}";
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        _stored!.RequestHash.Should().Be(hash);
        _values.Clear();
        (await Invoke(IdempotencyOperation.AddProduct)).Source.Should().Be(IdempotencyResultSource.Database);
    }

    [Fact]
    public async Task Execute_ShouldScopeRedisAndDatabaseByUserAndOperation()
    {
        await Invoke(IdempotencyOperation.AddProduct);
        await Invoke(IdempotencyOperation.UpdateProduct);
        _context.Setup(x => x.GetCurrent()).Returns(new ProductOperationContext("user-2", "second@example.com", "en", "correlation"));
        (await Invoke(IdempotencyOperation.AddProduct)).IsReplay.Should().BeFalse();
        _values.Should().HaveCount(3);
        _repository.Verify(x => x.GetAsync("user-2", IdempotencyOperation.AddProduct, _key, default), Times.Once);
    }

    #endregion

    #region Cache failures and invalid data

    [Fact]
    public async Task Execute_WhenRedisIsUnavailable_ShouldCommitAndReplayFromDatabaseWithoutAnotherCacheWrite()
    {
        _cache.Setup(x => x.GetAsync(It.IsAny<string>(), default)).ThrowsAsync(new IOException("Redis unavailable"));

        (await Invoke(IdempotencyOperation.UpdateProduct)).IsReplay.Should().BeFalse();
        var replay = await Invoke(IdempotencyOperation.UpdateProduct);

        replay.Source.Should().Be(IdempotencyResultSource.Database);
        _cache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenRedisWriteFails_ShouldReturnCommittedSuccessAndReplayOnRetry()
    {
        _cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default))
            .ThrowsAsync(new IOException("Redis write failed"));

        (await Invoke(IdempotencyOperation.DeleteProduct)).IsReplay.Should().BeFalse();
        (await Invoke(IdempotencyOperation.DeleteProduct)).Source.Should().Be(IdempotencyResultSource.Database);

        _stored.Should().NotBeNull();
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
        VerifyInner(IdempotencyOperation.DeleteProduct, Times.Once());
    }

    [Theory]
    [InlineData("json")]
    [InlineData("response")]
    [InlineData("null-response")]
    [InlineData("scope")]
    [InlineData("expired")]
    [InlineData("hash")]
    public async Task Execute_ShouldIgnoreAndRepairInvalidCacheValue(string damage)
    {
        await Invoke(IdempotencyOperation.UpdateProduct);
        string key = _values.Keys.Single();
        var json = JsonNode.Parse(_values[key])!;
        if (damage == "response") json["responseJson"] = "{}";
        if (damage == "null-response") json["responseJson"] = null;
        if (damage == "scope") json["userId"] = "another-user";
        if (damage == "expired") json["expiresAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(-1);
        if (damage == "hash") json["requestHash"] = new string('Z', 64);
        _values[key] = Encoding.UTF8.GetBytes(damage == "json" ? "broken" : json.ToJsonString());

        var replay = await Invoke(IdempotencyOperation.UpdateProduct);

        replay.Source.Should().Be(IdempotencyResultSource.Database);
        (await Invoke(IdempotencyOperation.UpdateProduct)).Source.Should().Be(IdempotencyResultSource.Redis);
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldReplayUncleanedExpiredDatabaseRecordWithoutCachingIt()
    {
        await Invoke(IdempotencyOperation.AddProduct);
        var json = JsonNode.Parse(JsonSerializer.Serialize(_stored, JsonSerializerOptions.Web))!;
        json["expiresAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(-1);
        _stored = json.Deserialize<IdempotencyRecord>(JsonSerializerOptions.Web);
        _values.Clear();

        (await Invoke(IdempotencyOperation.AddProduct)).Source.Should().Be(IdempotencyResultSource.Database);
        _values.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_WhenDatabaseLookupFails_ShouldNotExecuteBusinessOrWriteCache()
    {
        _repository.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<IdempotencyOperation>(), _key, default))
            .ThrowsAsync(new IOException("Database unavailable"));
        await FluentActions.Invoking(() => Invoke(IdempotencyOperation.AddProduct)).Should().ThrowAsync<IOException>();
        VerifyInner(IdempotencyOperation.AddProduct, Times.Never());
        _values.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_WhenCommitFails_ShouldDiscardChangesAndNeverPublishToRedis()
    {
        _unitOfWork.Setup(x => x.SaveChangesAsync(default)).ThrowsAsync(new IOException("Commit failed"));
        await FluentActions.Invoking(() => Invoke(IdempotencyOperation.AddProduct)).Should().ThrowAsync<IOException>();
        _unitOfWork.Verify(x => x.DiscardPendingChanges(), Times.Once);
        _stored.Should().BeNull();
        _pending.Should().BeNull();
        _values.Should().BeEmpty();
    }

    #endregion

    #region Concurrent results

    [Theory]
    [InlineData(IdempotencyOperation.AddProduct)]
    [InlineData(IdempotencyOperation.UpdateProduct)]
    [InlineData(IdempotencyOperation.DeleteProduct)]
    public async Task Execute_ShouldRecoverCommittedWinnerAfterSaveConflict(IdempotencyOperation operation)
    {
        await Invoke(operation);
        IdempotencyRecord winner = _stored!;
        _values.Clear();
        _repository.SetupSequence(x => x.GetAsync("user-1", operation, _key, default)).ReturnsAsync((IdempotencyRecord?)null).ReturnsAsync(winner);
        _unitOfWork.Setup(x => x.SaveChangesAsync(default)).ThrowsAsync(new IdempotencyRecordConflictException(new Exception()));

        var replay = await Invoke(operation);

        replay.Source.Should().Be(IdempotencyResultSource.Database);
        replay.IsReplay.Should().BeTrue();
        _pending.Should().BeNull();
        _unitOfWork.Verify(x => x.DiscardPendingChanges(), Times.Once);
        JsonSerializer.Deserialize<IdempotencyRecord>(_values.Values.Single(), JsonSerializerOptions.Web)!.Id.Should().Be(winner.Id);
    }

    [Theory]
    [InlineData(IdempotencyOperation.AddProduct)]
    [InlineData(IdempotencyOperation.UpdateProduct)]
    [InlineData(IdempotencyOperation.DeleteProduct)]
    public async Task Execute_ShouldRecoverWinnerWhenBusinessPrecheckLosesRace(IdempotencyOperation operation)
    {
        await Invoke(operation);
        IdempotencyRecord winner = _stored!;
        _values.Clear();
        _repository.SetupSequence(x => x.GetAsync("user-1", operation, _key, default)).ReturnsAsync((IdempotencyRecord?)null).ReturnsAsync(winner);
        if (operation == IdempotencyOperation.AddProduct)
            _adder.Setup(x => x.AddProductAsync(It.IsAny<ProductAddRequest>(), _key)).ThrowsAsync(new ProductAlreadyExistsException("Test"));
        if (operation == IdempotencyOperation.UpdateProduct)
            _updater.Setup(x => x.UpdateProductAsync(It.IsAny<ProductUpdateRequest>(), _key)).ThrowsAsync(new ProductConcurrencyException(_product.ProductId));
        if (operation == IdempotencyOperation.DeleteProduct)
            _deleter.Setup(x => x.DeleteProductAsync(It.IsAny<Guid>(), It.IsAny<int>(), _key)).ThrowsAsync(new ProductNotFoundException(_product.ProductId));

        (await Invoke(operation)).Source.Should().Be(IdempotencyResultSource.Database);
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Once);
        _unitOfWork.Verify(x => x.DiscardPendingChanges(), Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldKeepRealVersionConflictWhenThereIsNoMatchingRecord()
    {
        _updater.Setup(x => x.UpdateProductAsync(It.IsAny<ProductUpdateRequest>(), _key)).ThrowsAsync(new ProductConcurrencyException(_product.ProductId));
        await FluentActions.Invoking(() => Invoke(IdempotencyOperation.UpdateProduct)).Should().ThrowAsync<ProductConcurrencyException>();
        _unitOfWork.Verify(x => x.SaveChangesAsync(default), Times.Never);
        _values.Should().BeEmpty();
    }

    #endregion

    private async Task<(bool IsReplay, IdempotencyResultSource Source, ProductResponse? Product)> Invoke(IdempotencyOperation operation)
    {
        if (operation == IdempotencyOperation.AddProduct)
        {
            var result = await new ProductsAdderIdempotencyDecorator(_adder.Object, _executor)
                .AddProductAsync(new ProductAddRequest { DisplayName = _name, UnitPrice = 10, QuantityInStock = 2 }, _key);
            return (result.IsReplay, result.Source, result.Product);
        }
        if (operation == IdempotencyOperation.UpdateProduct)
        {
            var result = await new ProductsUpdaterIdempotencyDecorator(_updater.Object, _executor)
                .UpdateProductAsync(new ProductUpdateRequest { ProductId = _product.ProductId, DisplayName = _name, UnitPrice = 10, QuantityInStock = 2, Version = _version }, _key);
            return (result.IsReplay, result.Source, result.Product);
        }
        var deleted = await new ProductsDeleterIdempotencyDecorator(_deleter.Object, _executor).DeleteProductAsync(_product.ProductId, _version, _key);
        deleted.Deleted.Should().BeTrue();
        return (deleted.IsReplay, deleted.Source, null);
    }

    private void VerifyInner(IdempotencyOperation operation, Times times)
    {
        if (operation == IdempotencyOperation.AddProduct) _adder.Verify(x => x.AddProductAsync(It.IsAny<ProductAddRequest>(), _key), times);
        if (operation == IdempotencyOperation.UpdateProduct) _updater.Verify(x => x.UpdateProductAsync(It.IsAny<ProductUpdateRequest>(), _key), times);
        if (operation == IdempotencyOperation.DeleteProduct) _deleter.Verify(x => x.DeleteProductAsync(It.IsAny<Guid>(), It.IsAny<int>(), _key), times);
    }
}
