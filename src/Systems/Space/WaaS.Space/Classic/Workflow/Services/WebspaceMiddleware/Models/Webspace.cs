namespace WebspaceMiddleware;

public class Webspace : WebspaceState
{
    [JsonPropertyName("_actual")]
    public WebspaceState? ActualState { get; set; }

    [JsonPropertyName("_errors")]
    public IEnumerable<SpaceMiddleware.Error>? Errors { get; set; }

    public void Tombstone()
    {
    }
}
