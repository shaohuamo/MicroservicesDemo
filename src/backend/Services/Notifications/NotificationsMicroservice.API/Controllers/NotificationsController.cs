using CommonService.Middlewares;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NotificationsMicroservice.Core.DTO;
using NotificationsMicroservice.Core.ServiceContracts;
using NotificationsMicroservice.Infrastructure.Options;

namespace NotificationsMicroservice.API.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(
    INotificationsGetterService getterService,
    INotificationsUpdaterService updaterService,
    IOptions<DeliveryOptions> deliveryOptions) : ControllerBase
{
    // GET /api/notifications?limit=20
    /// <summary>
    /// Gets a page of notification history for the current user.
    /// </summary>
    /// <param name="beforeSequence">Returns notifications before this sequence number.</param>
    /// <param name="limit">The maximum number of notifications to return.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A page of notification history.</returns>
    [HttpGet]
    public async Task<ActionResult<NotificationHistoryPage>> GetHistory(
        [FromQuery] long? beforeSequence = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (beforeSequence is <= 0 || limit is < 1 or > 100)
        {
            return ValidationProblem("beforeSequence must be positive and limit must be between 1 and 100.");
        }

        return Ok(await getterService.GetHistoryAsync(
            GetRequiredUserId(),
            beforeSequence,
            limit,
            deliveryOptions.Value.RetentionDays,
            cancellationToken));
    }

    /// <summary>Replays all notifications without an in-app acknowledgement, oldest operation first.</summary>
    /// <param name="cursor">Opaque ascending page cursor returned by the previous replay page.</param>
    /// <param name="limit">Maximum number of notifications to return, from 1 through 100.</param>
    /// <param name="cancellationToken">Cancels the query if the HTTP request is aborted.</param>
    [HttpGet("replay")]
    public async Task<ActionResult<NotificationReplayPage>> Replay(
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        NotificationReplayCursor? boundary = null;
        if (limit is < 1 or > 100 || (cursor is not null && !NotificationReplayCursor.TryDecode(cursor, out boundary)))
        {
            return ValidationProblem(detail: "Replay cursor and limit are invalid.", statusCode: 400);
        }
        return Ok(await getterService.GetReplayAsync(GetRequiredUserId(), boundary, limit, cancellationToken));
    }

    // POST /api/notifications/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx/ack
    /// <summary>
    /// Acknowledges delivery of a notification for the current user.
    /// </summary>
    /// <param name="notificationId">The notification identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content when acknowledged; otherwise, not found.</returns>
    [HttpPost("{notificationId:guid}/ack")]
    public async Task<IActionResult> Acknowledge(Guid notificationId, CancellationToken cancellationToken)
    {
        var exists = await updaterService.AcknowledgeAsync(GetRequiredUserId(), notificationId, cancellationToken);
        return exists ? NoContent() : NotFound();
    }

    // PUT /api/notifications/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx/read
    /// <summary>
    /// Marks a notification as read for the current user.
    /// </summary>
    /// <param name="notificationId">The notification identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content when updated; otherwise, not found.</returns>
    [HttpPut("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        var exists = await updaterService.MarkReadAsync(GetRequiredUserId(), notificationId, cancellationToken);
        return exists ? NoContent() : NotFound();
    }

    // POST /api/notifications/read-all
    /// <summary>
    /// Marks all eligible notifications up to the specified sequence as read.
    /// </summary>
    /// <param name="request">The upper sequence boundary for the update.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The number of notifications updated.</returns>
    [HttpPost("read-all")]
    public async Task<ActionResult<object>> MarkAllRead(
        [FromBody] ReadAllNotificationsRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await updaterService.MarkAllReadAsync(
            GetRequiredUserId(),
            request.UpToSequence,
            cancellationToken);
        return Ok(new { updated });
    }

    private string GetRequiredUserId() =>
        HttpContext.Items[TraceContextMiddleware.UserIdItemKey] as string
        ?? throw new InvalidOperationException("The gateway user ID was not resolved.");
}
