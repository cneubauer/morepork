namespace WaaS.Common.ViewModel;

public struct Notification
{
    /// <summary>
    /// The unique identifier of the notification.
    /// </summary>
    /// <example>42</example>
    public long Id { get; set; }

    /// <summary>
    /// The ID of the job that produced this notification.
    /// </summary>
    /// <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
    public Guid? JobId { get; set; }

    /// <summary>
    /// The type name of the notification.
    /// </summary>
    /// <example>SpacePublished</example>
    public string? NotificationType { get; set; }

    /// <summary>
    /// An external reference ID passed by the client to correlate the notification.
    /// </summary>
    /// <example>ext-ref-12345</example>
    public string? ExternalReferenceId { get; set; }

    /// <summary>
    /// The correlation ID linking related notifications and jobs.
    /// </summary>
    /// <example>7c9e6679-7425-40de-944b-e07fc1f90ae7</example>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// The timestamp when the notification was created.
    /// </summary>
    /// <example>2024-06-15T10:30:00Z</example>
    public DateTime Created { get; set; }

    /// <summary>
    /// Payload structure depends on notification type.
    /// </summary>
    public object? Payload { get; set; }
}
