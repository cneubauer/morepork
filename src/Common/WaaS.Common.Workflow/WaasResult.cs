using WaaS.Persistence;

namespace WaaS.Common.Workflow;

public class WaasResult<TDesiredState>
{
    public List<string> ValidationErrors { get; set; } = [];
    public IDesiredState<TDesiredState>? DesiredState { get; set; }
}