namespace WaaS.Space.Worker;

public static class BackendModelExtensions
{
    public static string AsStatus(this IEnumerable<Common.DesiredState.LockItem> lockItems)
        => lockItems != null && lockItems.Any()
            ? "locked"
            : "enabled";
}
