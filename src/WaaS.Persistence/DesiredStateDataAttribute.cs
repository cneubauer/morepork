namespace WaaS.Persistence;

[AttributeUsage(AttributeTargets.Class)]
public class DesiredStateDataAttribute : Attribute
{
    public short Namespace { get; set; }
}