namespace WaaS.WebApi;

[ApiController]
[Route("api/{tenant}/eventing/notifications")]
public class NotificationController(INotificationStore notificationStore) : ControllerBase
{

    /// <summary>
    /// List notifications for resource operations.
    /// </summary>
    /// <remarks>
    /// This endpoint is called "history", but it actually is the primary endpoint to retrieve notifications for resource operations.
    /// </remarks>
    [HttpGet("history")]
    public async Task<IActionResult> ListNotifications([FromRoute] string tenant)
    {
        var notifications = await notificationStore.ListNotifications(tenant);

        return Ok(notifications);
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