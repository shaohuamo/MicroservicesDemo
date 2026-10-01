using CommonService.Middlewares;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using NotificationsMicroservice.API.Controllers;
using NotificationsMicroservice.Core.DTO;
using NotificationsMicroservice.Core.ServiceContracts;
using NotificationsMicroservice.Infrastructure.Options;

namespace NotificationsMicroservice.Tests;

public class NotificationsControllerTests
{
    private readonly Mock<INotificationsGetterService> _getterService = new();
    private readonly Mock<INotificationsUpdaterService> _updaterService = new();
    private readonly NotificationsController _controller;

    public NotificationsControllerTests()
    {
        _controller = new NotificationsController(
            _getterService.Object,
            _updaterService.Object,
            Options.Create(new DeliveryOptions { RetentionDays = 30 }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        _controller.HttpContext.Items[TraceContextMiddleware.UserIdItemKey] = "current-user";
    }

    [Fact]
    public async Task GetHistory_AlwaysUsesAuthenticatedSubject()
    {
        var page = new NotificationHistoryPage([], null, 0, 0);
        _getterService.Setup(x => x.GetHistoryAsync("current-user", null, 20, 30, default))
            .ReturnsAsync(page);

        var result = await _controller.GetHistory(cancellationToken: default);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(page);
        _getterService.VerifyAll();
    }

    [Fact]
    public async Task Acknowledge_ReturnsNotFoundWithoutExposingAnotherUsersRecord()
    {
        var notificationId = Guid.NewGuid();
        _updaterService.Setup(x => x.AcknowledgeAsync("current-user", notificationId, default))
            .ReturnsAsync(false);

        (await _controller.Acknowledge(notificationId, default)).Should().BeOfType<NotFoundResult>();
        _updaterService.VerifyAll();
    }

    [Fact]
    public async Task MarkRead_IsIdempotentForAnExistingNotification()
    {
        var notificationId = Guid.NewGuid();
        _updaterService.Setup(x => x.MarkReadAsync("current-user", notificationId, default))
            .ReturnsAsync(true);

        (await _controller.MarkRead(notificationId, default)).Should().BeOfType<NoContentResult>();
        _updaterService.VerifyAll();
    }

    [Fact]
    public async Task MarkAllRead_UsesClientWatermarkAndAuthenticatedSubject()
    {
        _updaterService.Setup(x => x.MarkAllReadAsync("current-user", 42, default)).ReturnsAsync(3);

        var result = await _controller.MarkAllRead(new ReadAllNotificationsRequest(42), default);

        result.Result.Should().BeOfType<OkObjectResult>();
        _updaterService.VerifyAll();
    }

    [Fact]
    public async Task Replay_WithoutCursor_UsesAuthenticatedSubjectAndFullRange()
    {
        var page = new NotificationReplayPage([], null, 100);
        _getterService.Setup(value => value.GetReplayAsync("current-user", null, 100, default)).ReturnsAsync(page);

        (await _controller.Replay()).Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(page);
        _getterService.VerifyAll();
    }

    [Fact]
    public async Task Replay_WithCursor_PassesDecodedBoundary()
    {
        var cursor = new NotificationReplayCursor(100, DateTimeOffset.UtcNow, 53);
        var page = new NotificationReplayPage([], null, 100);
        _getterService.Setup(value => value.GetReplayAsync("current-user", cursor, 20, default)).ReturnsAsync(page);

        (await _controller.Replay(cursor.Encode(), 20)).Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(page);
        _getterService.VerifyAll();
    }

    [Theory]
    [InlineData("invalid", 100)]
    [InlineData("", 100)]
    [InlineData(null, 0)]
    [InlineData(null, 101)]
    public async Task Replay_WithInvalidCursorOrLimit_ReturnsBadRequest(string? cursor, int limit)
    {
        var result = await _controller.Replay(cursor, limit);
        result.Result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(400);
        _getterService.Verify(value => value.GetReplayAsync(It.IsAny<string>(), It.IsAny<NotificationReplayCursor?>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Replay_WithLegacyDescendingCursor_ReturnsBadRequestWithoutQuerying()
    {
        var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            "{\"Watermark\":100,\"BeforeSequence\":53,\"BeforeOccurredAtUtc\":\"2026-10-02T00:00:00Z\"}"));

        var result = await _controller.Replay(token);

        result.Result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(400);
        _getterService.Verify(value => value.GetReplayAsync(It.IsAny<string>(), It.IsAny<NotificationReplayCursor?>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
