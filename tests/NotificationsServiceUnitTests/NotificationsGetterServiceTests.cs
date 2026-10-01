using FluentAssertions;
using Moq;
using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Core.DTO;
using NotificationsMicroservice.Core.Services;

namespace NotificationsMicroservice.Tests;

public sealed class NotificationsGetterServiceTests
{
    private readonly Mock<INotificationGetRepository> _repository = new();
    private readonly NotificationsGetterService _service;

    public NotificationsGetterServiceTests()
    {
        _service = new NotificationsGetterService(_repository.Object);
    }

    [Fact]
    public async Task GetHistoryAsync_DelegatesToGetRepository()
    {
        var page = new NotificationHistoryPage([], null, 0, 0);
        _repository.Setup(repository => repository.GetHistoryAsync("user", 5, 20, 30, default))
            .ReturnsAsync(page);

        (await _service.GetHistoryAsync("user", 5, 20, 30, default)).Should().BeSameAs(page);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task GetReplayAsync_DelegatesDescendingCursorAndCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var cursor = new NotificationReplayCursor(100, DateTimeOffset.UtcNow, 50);
        var page = new NotificationReplayPage([], null, 100);
        _repository.Setup(repository => repository.GetReplayAsync("user", cursor, 20, cancellation.Token)).ReturnsAsync(page);

        (await _service.GetReplayAsync("user", cursor, 20, cancellation.Token)).Should().BeSameAs(page);
        _repository.VerifyAll();
    }
}
