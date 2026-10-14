namespace WaaS.Persistence;

public interface INotificationStore
{
    Task<Notification> WriteNotification(NotificationType notificationType, IDesiredState desiredState, object? payload = null);
    Task<IEnumerable<Notification>> ListNotifications(short tenantId, string username, NotificationFilter filter);
    Task<long> GetNotificationPointer(string username);
    Task SetNotificationPointer(string username, long notificationId);

}