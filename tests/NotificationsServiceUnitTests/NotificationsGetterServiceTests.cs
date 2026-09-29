using FluentAssertions;
using Moq;
using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Core.DTO;
using NotificationsMicroservice.Core.Services;

namespace NotificationsMicroservice.Tests;

public sealed class NotificationsGetterServiceTests
{
    private readonly Mock<INotificationReadRepository> _repository = new();
    private readonly NotificationsGetterService _service;

    public NotificationsGetterServiceTests()
    {
        _service = new NotificationsGetterService(_repository.Object);
    }

    [Fact]
    public async Task GetHistoryAsync_DelegatesToReadRepository()
    {
        var page = new NotificationHistoryPage([], null, 0, 0);
        _repository.Setup(repository => repository.GetHistoryAsync("user", 5, 20, 30, default))
            .ReturnsAsync(page);

        (await _service.GetHistoryAsync("user", 5, 20, 30, default)).Should().BeSameAs(page);
        _repository.VerifyAll();
    }
}
