using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NotificationsMicroservice.Core.Domain.RepositoryContracts;
using NotificationsMicroservice.Infrastructure.HostedServices;
using NotificationsMicroservice.Infrastructure.Options;

namespace NotificationsMicroservice.Tests;

public sealed class NotificationCleanupWorkerTests
{
    [Fact]
    public async Task StartAsync_ImmediatelyCleansExpiredNotifications()
    {
        var repository = new Mock<INotificationDeleteRepository>();
        var cleaned = new TaskCompletionSource<DateTimeOffset>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.Setup(value => value.DeleteCompletedBeforeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<DateTimeOffset, CancellationToken>((cutoff, _) => cleaned.TrySetResult(cutoff))
            .ReturnsAsync(0);
        using var cancellation = new CancellationTokenSource();
        var worker = CreateWorker(repository.Object, retentionDays: 30, pollingIntervalMilliseconds: 10);

        await worker.StartAsync(cancellation.Token);
        var cutoff = await cleaned.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();
        await worker.StopAsync(CancellationToken.None);

        cutoff.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(-30), TimeSpan.FromSeconds(2));
        repository.Verify(value => value.DeleteCompletedBeforeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CleanupFailure_RetriesUsingPollingInterval()
    {
        var repository = new Mock<INotificationDeleteRepository>();
        var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        repository.Setup(value => value.DeleteCompletedBeforeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns((DateTimeOffset _, CancellationToken _) =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    return Task.FromException<int>(new InvalidOperationException("Database unavailable"));
                }

                retried.TrySetResult();
                return Task.FromResult(0);
            });
        using var cancellation = new CancellationTokenSource();
        var worker = CreateWorker(repository.Object, retentionDays: 30, pollingIntervalMilliseconds: 10);

        await worker.StartAsync(cancellation.Token);
        await retried.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();
        await worker.StopAsync(CancellationToken.None);

        repository.Verify(value => value.DeleteCompletedBeforeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static NotificationCleanupWorker CreateWorker(
        INotificationDeleteRepository repository,
        int retentionDays,
        int pollingIntervalMilliseconds) =>
        new(
            repository,
            Options.Create(new DeliveryOptions
            {
                RetentionDays = retentionDays,
                PollingIntervalMilliseconds = pollingIntervalMilliseconds,
                CleanupIntervalMinutes = 1
            }),
            NullLogger<NotificationCleanupWorker>.Instance);
}
