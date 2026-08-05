using WaaS.Persistence;

namespace WaaS.Common.Workflow;

public class WaasResult<TDesiredState> where TDesiredState : IDesiredStateData, new()
{
    public List<string> ValidationErrors { get; set; } = [];
    public DesiredState<TDesiredState>? DesiredState { get; set; }
}