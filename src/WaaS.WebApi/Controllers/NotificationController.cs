namespace WaaS.WebApi;

[ApiController]
[Route("api/{tenant}/eventing/notifications")]
public class NotificationController(INotificationStore notificationStore, ITenantStore tenantStore) : ControllerBase
{
    private const int MaxTimeToWait = ListNotificationsQuery.DefaultTimeToWait;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// List notifications for resource operations.
    /// </summary>
    /// <remarks>
    /// This endpoint is called "history", but it actually is the primary endpoint to retrieve notifications for resource operations.
    /// If no notification matches, the request waits up to <c>TimeToWait</c> seconds for one to arrive.
    /// </remarks>
    /// <param name="tenant" example="demo">The tenant identifier.</param>
    /// <param name="query">The filter and long-polling options.</param>
    /// <param name="cancellationToken">Cancels the wait when the client disconnects.</param>
    [HttpGet("history")]
    public async Task<IActionResult> ListNotifications(
        [FromRoute] string tenant,
        [FromQuery] ListNotificationsQuery query,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await tenantStore.Read(tenant);

        if (tenantEntity is null)
            return NotFound();

        var filter = new NotificationFilter
        {
            NotificationIds = query.NotificationIds ?? [],
            StartNotificationId = query.StartNotificationId,
            NotificationTypes = query.NotificationType ?? [],
            StackInstanceIds = query.StackInstanceId ?? [],
            SystemInstanceIds = query.SystemInstanceId ?? [],
            Namespaces = (query.NamespaceType ?? []).Select(x => (short)x).ToArray(),
            Zones = (query.Zone ?? []).Select(x => (short)x).ToArray(),
            TimeStamp = query.TimeStamp,
            Limit = query.Limit,
            TransactionId = query.TransactionId,
        };

        var deadline = DateTime.UtcNow.AddSeconds(Math.Clamp(query.TimeToWait, 0, MaxTimeToWait));

        while (true)
        {
            var notifications = (await notificationStore.ListNotifications(tenantEntity.Id, tenant, filter)).ToList();

            if (notifications.Count > 0 || DateTime.UtcNow >= deadline)
                return Ok(notifications);

            try
            {
                // The connection is back in the pool while we wait.
                await Task.Delay(PollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new EmptyResult();
            }
        }
    }

    /// <summary>
    /// Set the pointer for notifications.
    /// </summary>
    /// <returns>Acknowledgment of the pointer being set.</returns>
    [HttpPost("pointer/{notificationId}")]
    public async Task<IActionResult> SetPointer([FromRoute] string tenant, [FromRoute] long notificationId)
    {
        await notificationStore.SetNotificationPointer(tenant, notificationId);

        return NoContent();
    }
}

public record ListNotificationsQuery
{
    public const int DefaultTimeToWait = 60;

    /// <summary>Seconds to wait for a notification if none is available. Defaults to 60, which is also the maximum.</summary>
    public int TimeToWait { get; init; } = DefaultTimeToWait;

    /// <summary>List of notification ids to retrieve.</summary>
    public long[]? NotificationIds { get; init; }

    /// <summary>ID of the first notification to retrieve.</summary>
    public long? StartNotificationId { get; init; }

    /// <summary>The Stack Instance IDs of the related resources.</summary>
    public long[]? StackInstanceId { get; init; }

    /// <summary>The System Instance IDs of the related resources.</summary>
    public long[]? SystemInstanceId { get; init; }

    /// <summary>The types of the notifications to retrieve.</summary>
    public NotificationType[]? NotificationType { get; init; }

    /// <summary>The zones of the related resources.</summary>
    public ZoneType[]? Zone { get; init; }

    /// <summary>The namespaces of the related resources.</summary>
    public NamespaceType[]? NamespaceType { get; init; }

    /// <summary>Only retrieve notifications created at or after this time.</summary>
    public DateTimeOffset? TimeStamp { get; init; }

    /// <summary>Limit the result to the specified number of notifications.</summary>
    public int? Limit { get; init; }

    /// <summary>Retrieve notifications with the specified transaction ID.</summary>
    public string? TransactionId { get; init; }
}
