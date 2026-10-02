using ObjectCompare;

namespace WaaS.Common.Workflow;

public record WaasContext
{
    public required string TransactionId { get; init; }
    public List<string> ValidationErrors { get; init; } = [];
    public required Tenant Tenant { get; init; }
    public required StackInstance StackInstance { get; init; }
}

public record WaasContext<TDesiredState> : WaasContext where TDesiredState : IDesiredStateData, new()
{
    public required DesiredState<TDesiredState> DesiredState { get; init; }
}

public record ProcessingContext<TDesiredState> : WaasContext<TDesiredState> where TDesiredState : IDesiredStateData, new()
{
    public required IReadOnlyList<IChange> Changes { get; init; }
}