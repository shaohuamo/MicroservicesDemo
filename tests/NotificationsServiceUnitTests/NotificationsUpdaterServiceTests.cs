using FluentAssertions;
using Moq;
using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Core.Services;

namespace NotificationsMicroservice.Tests;

public sealed class NotificationsUpdaterServiceTests
{
    private readonly Mock<INotificationUpdateRepository> _repository = new();
    private readonly NotificationsUpdaterService _service;

    public NotificationsUpdaterServiceTests()
    {
        _service = new NotificationsUpdaterService(_repository.Object);
    }

    [Fact]
    public async Task MarkAllReadAsync_DelegatesToUpdateRepository()
    {
        _repository.Setup(repository => repository.MarkAllReadAsync("user", 42, default)).ReturnsAsync(3);

        (await _service.MarkAllReadAsync("user", 42, default)).Should().Be(3);
        _repository.VerifyAll();
    }
}
