using WaaS.Persistence;

namespace WaaS.Common.Workflow;

public class WaasResult<TDesiredState> where TDesiredState : IDesiredStateData
{
    public List<string> ValidationErrors { get; set; } = [];
    public IDesiredState<TDesiredState>? DesiredState { get; set; }
}