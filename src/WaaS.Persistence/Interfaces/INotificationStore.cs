namespace WaaS.Persistence;

public interface INotificationStore
{
    Task<Notification> WriteNotification(NotificationType notificationType, string transactionId, object? payload = null);
    Task<IEnumerable<Notification>> ListNotifications(string username);
    Task<long> GetNotificationPointer(string username);
    Task SetNotificationPointer(string username, long notificationId);

}