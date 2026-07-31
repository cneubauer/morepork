namespace MyNamespace;

public class WaasResult
{
    public List<string> ValidationErrors { get; set; } = [];
    public DesiredState? DesiredState { get; set; }
}