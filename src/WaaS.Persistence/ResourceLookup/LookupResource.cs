namespace WaaS.Persistence;

internal class LookupResource
{
    public long StackInstanceId { get; set; }
    public long SystemInstanceId { get; set; }
    public short Namespace { get; set; }
    public short Zone { get; set; }
    public short Tenant { get; set; }
    public short ResourceKey { get; set; }
    public string Text { get; set; } = "";
    public string TextReverse { get; set; } = "";
}
