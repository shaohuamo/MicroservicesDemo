using Dapper;
using FluentAssertions;
using NotificationsMicroservice.Core.DTO;
using NotificationsMicroservice.Infrastructure.Persistence;

namespace NotificationsServiceIntegrationTests;

public sealed class NotificationReplayAndAckTests(NotificationsDatabaseFixture database)
    : IClassFixture<NotificationsDatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var connection = await database.Factory.OpenConnectionAsync(default);
        await connection.ExecuteAsync("TRUNCATE public.\"Notifications\" RESTART IDENTITY");
    }
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Pending")]
    [InlineData("AwaitingSseAck")]
    [InlineData("RetryScheduled")]
    [InlineData("SendingEmail")]
    [InlineData("DeliveredEmail")]
    [InlineData("Failed")]
    public async Task Replay_IncludesEveryStateWithoutAnInAppAck(string status)
    {
        var row = await InsertAsync(status);
        var page = await database.GetRepository.GetReplayAsync("user", null, 100, default);
        page.Items.Select(item => item.NotificationId).Should().Equal(row.NotificationId);
        (await database.UpdateRepository.AcknowledgeAsync("user", row.NotificationId, default)).Should().BeTrue();
        (await database.GetRepository.GetReplayAsync("user", null, 100, default)).Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("AwaitingSseAck")]
    [InlineData("RetryScheduled")]
    [InlineData("SendingEmail")]
    [InlineData("DeliveredEmail")]
    [InlineData("Failed")]
    public async Task Ack_RepeatedCalls_PreserveTheFirstAckAndDoNotRepeatStateChanges(string status)
    {
        var row = await InsertAsync(status);
        await database.UpdateRepository.AcknowledgeAsync("user", row.NotificationId, default);
        var first = await StateAsync(row.NotificationId);
        first.InAppAcknowledgedAtUtc.Should().NotBeNull();
        first.DeliveryStatus.Should().Be(status is "SendingEmail" or "DeliveredEmail" ? status : "DeliveredInApp");
        if (status is "SendingEmail" or "DeliveredEmail") first.Version.Should().Be(7);
        else first.Version.Should().Be(8);
        if (status == "SendingEmail") first.LockedBy.Should().Be("worker");

        await database.UpdateRepository.AcknowledgeAsync("user", row.NotificationId, default);
        (await StateAsync(row.NotificationId)).Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task ReplayAndAck_IsolateUsers()
    {
        var own = await InsertAsync("Pending");
        var other = await InsertAsync("Pending", userId: "other");
        var page = await database.GetRepository.GetReplayAsync("user", null, 100, default);
        page.Items.Select(item => item.NotificationId).Should().Equal(own.NotificationId);
        (await database.UpdateRepository.AcknowledgeAsync("user", other.NotificationId, default)).Should().BeFalse();
        (await database.UpdateRepository.AcknowledgeAsync("user", Guid.NewGuid(), default)).Should().BeFalse();
        (await StateAsync(other.NotificationId)).InAppAcknowledgedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task AscendingPagination_PreservesMicrosecondsTiesAndWatermarkAcrossAckAndDeletion()
    {
        var rows = new List<Inserted>();
        var timestamp = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
        for (var index = 0; index < 250; index++)
            rows.Add(await InsertAsync("Pending", occurredAt: timestamp.AddTicks(index % 5 * 10)));
        var expected = rows.OrderBy(row => row.OccurredAtUtc).ThenBy(row => row.SequenceNumber).ToList();
        var page = await database.GetRepository.GetReplayAsync("user", null, 80, default);
        var watermark = page.Watermark;
        var received = page.Items.Select(item => item.NotificationId).ToList();
        await database.UpdateRepository.AcknowledgeAsync("user", page.Items[0].NotificationId, default);
        await using (var connection = await database.Factory.OpenConnectionAsync(default))
            await connection.ExecuteAsync("DELETE FROM public.\"Notifications\" WHERE \"NotificationId\" = @Id", new { Id = page.Items[^1].NotificationId });
        var late = await InsertAsync("Pending", occurredAt: timestamp.AddDays(-1));
        late.SequenceNumber.Should().BeGreaterThan(watermark);

        while (page.NextCursor is not null)
        {
            NotificationReplayCursor.TryDecode(page.NextCursor, out var cursor).Should().BeTrue();
            page = await database.GetRepository.GetReplayAsync("user", cursor, 80, default);
            page.Watermark.Should().Be(watermark);
            received.AddRange(page.Items.Select(item => item.NotificationId));
        }
        received.Should().Equal(expected.Select(row => row.NotificationId));
        received.Should().OnlyHaveUniqueItems();
        received.Should().NotContain(late.NotificationId);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("retry")]
    [InlineData("fail")]
    public async Task AckDuringEmail_DoesNotBreakEmailCompletionOrRestartDelivery(string transition)
    {
        var row = await InsertAsync("SendingEmail");
        await database.UpdateRepository.AcknowledgeAsync("user", row.NotificationId, default);
        var repository = database.UpdateRepository;
        var changed = transition switch
        {
            "complete" => await repository.MarkDeliveredEmailAsync(row.NotificationId, 7, "worker", "provider-id", default),
            "retry" => await repository.ScheduleRetryAsync(row.NotificationId, 7, "worker", DateTimeOffset.UtcNow.AddMinutes(1), "ERROR", default),
            _ => await repository.MarkFailedAsync(row.NotificationId, 7, "worker", "ERROR", default),
        };
        changed.Should().BeTrue();
        var state = await StateAsync(row.NotificationId);
        state.DeliveryStatus.Should().Be(transition == "complete" ? "DeliveredEmail" : "DeliveredInApp");
        state.DeliveredAtUtc.Should().NotBeNull();
        (await database.GetRepository.GetReplayAsync("user", null, 100, default)).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task AckBeforeEmail_BlocksTheEmailTransition()
    {
        var row = await InsertAsync("Pending");
        await database.UpdateRepository.AcknowledgeAsync("user", row.NotificationId, default);
        (await database.UpdateRepository.BeginSendingEmailAsync(row.NotificationId, 7, "worker", default)).Should().BeNull();
    }

    [Fact]
    public async Task MarkReadAndReadAll_ConfirmInAppDeliveryWithoutChangingAnEmailLease()
    {
        var sending = await InsertAsync("SendingEmail");
        var email = await InsertAsync("DeliveredEmail");
        await database.UpdateRepository.MarkReadAsync("user", sending.NotificationId, default);
        var state = await StateAsync(sending.NotificationId);
        state.InAppAcknowledgedAtUtc.Should().NotBeNull();
        state.ReadAtUtc.Should().NotBeNull();
        state.Version.Should().Be(7);
        state.LockedBy.Should().Be("worker");
        await database.UpdateRepository.MarkAllReadAsync("user", email.SequenceNumber, default);
        (await database.GetRepository.GetReplayAsync("user", null, 100, default)).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SchemaInitialization_CreatesAckColumnAndDescendingPartialIndex_AndCanRunTwice()
    {
        var row = await InsertAsync("Pending");
        var before = await StateAsync(row.NotificationId);

        await database.InitializeSchemaAsync();
        await database.InitializeSchemaAsync();

        (await StateAsync(row.NotificationId)).Should().BeEquivalentTo(before);
        await using var connection = await database.Factory.OpenConnectionAsync(default);
        var ackColumnExists = await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'Notifications'
                  AND column_name = 'InAppAcknowledgedAtUtc'
                  AND data_type = 'timestamp with time zone' AND is_nullable = 'YES');
            """);
        ackColumnExists.Should().BeTrue();
        var indexDefinition = await connection.QuerySingleAsync<string>(
            """
            SELECT indexdef FROM pg_indexes
            WHERE schemaname = 'public' AND tablename = 'Notifications'
              AND indexname = 'IX_Notifications_Unacknowledged_OperationTime';
            """);
        indexDefinition.Should().Contain("(\"UserId\", \"OccurredAtUtc\" DESC, \"SequenceNumber\" DESC)")
            .And.Contain("WHERE (\"InAppAcknowledgedAtUtc\" IS NULL)");
    }

    [Fact]
    public async Task VerifyConnection_DoesNotModifyNotificationData()
    {
        var inApp = await InsertAsync("DeliveredInApp");
        var read = await InsertAsync("Pending");
        var email = await InsertAsync("DeliveredEmail");
        await using (var connection = await database.Factory.OpenConnectionAsync(default))
        {
            await connection.ExecuteAsync("UPDATE public.\"Notifications\" SET \"ReadAtUtc\" = CURRENT_TIMESTAMP WHERE \"NotificationId\" = @Id", new { Id = read.NotificationId });
        }
        var beforeInApp = await StateAsync(inApp.NotificationId);
        var beforeRead = await StateAsync(read.NotificationId);
        var beforeEmail = await StateAsync(email.NotificationId);
        var verifier = new NotificationDatabaseVerifier(database.Factory);
        await verifier.VerifyConnectionAsync(default);
        await verifier.VerifyConnectionAsync(default);

        (await StateAsync(inApp.NotificationId)).Should().BeEquivalentTo(beforeInApp);
        (await StateAsync(read.NotificationId)).Should().BeEquivalentTo(beforeRead);
        (await StateAsync(email.NotificationId)).Should().BeEquivalentTo(beforeEmail);
    }

    private async Task<Inserted> InsertAsync(string status, string userId = "user", DateTimeOffset? occurredAt = null)
    {
        await using var connection = await database.Factory.OpenConnectionAsync(default);
        return await connection.QuerySingleAsync<Inserted>(
            """
            INSERT INTO public."Notifications" ("NotificationId", "PayloadHash", "UserId", "UserEmail", "Culture",
                "Operation", "Status", "OccurredAtUtc", "DeliveryStatus", "Version", "LockedBy", "LockedUntilUtc")
            VALUES (@Id, 'hash', @UserId, 'user@example.com', 'en', 'Add', 'Success', @OccurredAt, @Status, 7,
                'worker', CURRENT_TIMESTAMP + INTERVAL '30 seconds')
            RETURNING "NotificationId", "SequenceNumber", "OccurredAtUtc";
            """, new { Id = Guid.NewGuid(), UserId = userId, OccurredAt = occurredAt ?? DateTimeOffset.UtcNow, Status = status });
    }
    private async Task<Snapshot> StateAsync(Guid id)
    {
        await using var connection = await database.Factory.OpenConnectionAsync(default);
        return await connection.QuerySingleAsync<Snapshot>(
            "SELECT \"DeliveryStatus\", \"Version\", \"LockedBy\", \"InAppAcknowledgedAtUtc\", \"DeliveredAtUtc\", \"ReadAtUtc\" FROM public.\"Notifications\" WHERE \"NotificationId\" = @Id", new { Id = id });
    }
    private sealed class Inserted
    {
        public Guid NotificationId { get; init; }
        public long SequenceNumber { get; init; }
        public DateTimeOffset OccurredAtUtc { get; init; }
    }
    private sealed class Snapshot
    {
        public string DeliveryStatus { get; init; } = "";
        public long Version { get; init; }
        public string? LockedBy { get; init; }
        public DateTimeOffset? InAppAcknowledgedAtUtc { get; init; }
        public DateTimeOffset? DeliveredAtUtc { get; init; }
        public DateTimeOffset? ReadAtUtc { get; init; }
    }
}
