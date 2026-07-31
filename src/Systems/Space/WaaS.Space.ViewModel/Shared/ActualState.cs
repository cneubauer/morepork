using System.ComponentModel;

namespace WaaS.Space.ViewModel;

public class ActualState
{
    /// <summary>
    /// The placement tags currently active on the backend (actual state).
    /// </summary>
    [ReadOnly(true)]
    public IEnumerable<string>? PlacementTags { get; set; }

    /// <summary>
    /// The resource limits currently active on the backend (actual state).
    /// </summary>
    [ReadOnly(true)]
    public Limits? Limits { get; set; }
}