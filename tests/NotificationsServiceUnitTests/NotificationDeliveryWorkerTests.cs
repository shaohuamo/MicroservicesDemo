using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NotificationsMicroservice.Core.Abstractions;
using NotificationsMicroservice.Core.Domain;
using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Infrastructure.HostedServices;
using NotificationsMicroservice.Infrastructure.Options;

namespace NotificationsMicroservice.Tests;

public sealed class NotificationDeliveryWorkerTests
{
    private readonly Mock<INotificationUpdateRepository> _repository = new();
    private readonly Mock<IPresencePublisher> _publisher = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();

    #region Redis fallback and email grace

    [Fact]
    public async Task RedisFailure_BeforeGraceDeadline_SchedulesRetryWithoutStartingEmail()
    {
        var notification = NewNotification(DateTimeOffset.UtcNow, attemptCount: 1);
        var scheduled = new TaskCompletionSource<(DateTimeOffset At, string Error)>(TaskCreationOptions.RunContinuationsAsynchronously);
        ClaimOnce(notification);
        _publisher.Setup(value => value.GetActiveBffInstancesAsync(notification.UserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));
        _repository.Setup(value => value.ScheduleRetryAsync(notification.NotificationId, notification.Version,
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, long, string, DateTimeOffset, string, CancellationToken>((_, _, _, at, error, _) =>
                scheduled.TrySetResult((at, error)))
            .ReturnsAsync(true);

        await RunUntilAsync(scheduled.Task);

        var result = await scheduled.Task;
        result.At.Should().BeCloseTo(notification.CreatedAtUtc.AddSeconds(20), TimeSpan.FromMilliseconds(100));
        result.Error.Should().Be("REDIS_DELIVERY_FAILED");
        _repository.Verify(value => value.BeginSendingEmailAsync(It.IsAny<Guid>(), It.IsAny<long>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoPresence_BeforeGraceDeadline_AllowsDatabaseFallback()
    {
        var notification = NewNotification(DateTimeOffset.UtcNow, attemptCount: 1);
        var scheduled = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        ClaimOnce(notification);
        _publisher.Setup(value => value.GetActiveBffInstancesAsync(notification.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repository.Setup(value => value.ScheduleRetryAsync(notification.NotificationId, notification.Version,
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, long, string, DateTimeOffset, string, CancellationToken>((_, _, _, _, error, _) =>
                scheduled.TrySetResult(error))
            .ReturnsAsync(true);

        await RunUntilAsync(scheduled.Task);

        (await scheduled.Task).Should().Be("AWAITING_IN_APP_DELIVERY");
        _repository.Verify(value => value.BeginSendingEmailAsync(It.IsAny<Guid>(), It.IsAny<long>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GraceExpired_WithoutInAppAck_BeginsEmail(bool redisFails)
    {
        var notification = NewNotification(DateTimeOffset.UtcNow.AddSeconds(-21), attemptCount: 2);
        var emailStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ClaimOnce(notification);
        var lookup = _publisher.Setup(value => value.GetActiveBffInstancesAsync(notification.UserId, It.IsAny<CancellationToken>()));
        if (redisFails) lookup.ThrowsAsync(new InvalidOperationException("Redis unavailable"));
        else lookup.ReturnsAsync([]);
        _repository.Setup(value => value.BeginSendingEmailAsync(notification.NotificationId, notification.Version,
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => emailStarted.TrySetResult())
            // A missing version represents an ACK that won the database race.
            .ReturnsAsync((long?)null);

        await RunUntilAsync(emailStarted.Task);

        _repository.Verify(value => value.BeginSendingEmailAsync(notification.NotificationId, notification.Version,
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(value => value.ScheduleRetryAsync(It.IsAny<Guid>(), It.IsAny<long>(),
            It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _scopeFactory.Verify(value => value.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task GraceExpired_WithoutAck_SendsEmail()
    {
        var notification = NewNotification(DateTimeOffset.UtcNow.AddSeconds(-21), attemptCount: 2);
        var emailSender = new Mock<INotificationEmailSender>();
        var emailDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var services = new ServiceCollection()
            .AddSingleton(emailSender.Object)
            .BuildServiceProvider();
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(value => value.ServiceProvider).Returns(services);
        _scopeFactory.Setup(value => value.CreateScope()).Returns(scope.Object);
        ClaimOnce(notification);
        _publisher.Setup(value => value.GetActiveBffInstancesAsync(notification.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repository.Setup(value => value.BeginSendingEmailAsync(notification.NotificationId, notification.Version,
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification.Version + 1);
        emailSender.Setup(value => value.SendAsync(notification, It.IsAny<CancellationToken>()))
            .ReturnsAsync("provider-message-id");
        _repository.Setup(value => value.MarkDeliveredEmailAsync(notification.NotificationId, notification.Version + 1,
                It.IsAny<string>(), "provider-message-id", It.IsAny<CancellationToken>()))
            .Callback(() => emailDelivered.TrySetResult())
            .ReturnsAsync(true);

        await RunUntilAsync(emailDelivered.Task);

        emailSender.Verify(value => value.SendAsync(notification, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(value => value.MarkDeliveredEmailAsync(notification.NotificationId, notification.Version + 1,
            It.IsAny<string>(), "provider-message-id", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    private void ClaimOnce(Notification notification)
    {
        var claims = 0;
        _repository.Setup(value => value.ClaimDueAsync(It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Interlocked.Increment(ref claims) == 1 ? [notification] : []);
    }

    private async Task RunUntilAsync(Task completed)
    {
        using var cancellation = new CancellationTokenSource();
        var worker = new NotificationDeliveryWorker(
            _repository.Object,
            _publisher.Object,
            _scopeFactory.Object,
            Options.Create(new DeliveryOptions { PollingIntervalMilliseconds = 10, InAppGraceSeconds = 20 }),
            NullLogger<NotificationDeliveryWorker>.Instance);
        await worker.StartAsync(cancellation.Token);
        await completed.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await worker.StopAsync(CancellationToken.None);
    }

    private static Notification NewNotification(DateTimeOffset createdAt, int attemptCount) => new()
    {
        NotificationId = Guid.NewGuid(),
        SequenceNumber = 10,
        PayloadHash = "hash",
        UserId = "user-1",
        UserEmail = "user@example.com",
        Culture = "en",
        Operation = "Add",
        Status = "Success",
        DeliveryStatus = NotificationDeliveryStatus.Pending,
        AttemptCount = attemptCount,
        Version = 2,
        CreatedAtUtc = createdAt,
        OccurredAtUtc = createdAt,
    };
}
