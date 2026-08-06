using WaaS.Persistence;

namespace WaaS.Common.Workflow;

public record WaasContext<TDesiredState> where TDesiredState : IDesiredStateData, new()
{
    public required string TransactionId { get; init; }
    public List<string> ValidationErrors { get; init; } = [];
    public required Tenant Tenant { get; init; }
    public required IStackInstance StackInstance { get; init; }
    public required IDesiredState<TDesiredState> DesiredState { get; init; }
}